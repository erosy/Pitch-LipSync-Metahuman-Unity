"""Bake Ethan's fitted, head-local groom. Requires NumPy, SciPy and Pillow."""
import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
from scipy.spatial import cKDTree


ROOT = Path(__file__).resolve().parents[2]
GROOM = ROOT / "Assets/_Sandbox/Characters/Ethan_V2/Groom"
OUTPUT = ROOT / "Assets/_Sandbox/EthanHairComparison"
ATLAS_SIZE = 2048
SHELL_HEIGHT = 504
CARD_COUNT = 480
CENTER = np.array([-0.105, 0.0, 0.008])


def unit(vectors):
    return vectors / np.maximum(np.linalg.norm(vectors, axis=-1, keepdims=True), 1e-9)


def spherical_uv(points):
    d = unit(points - CENTER)
    return np.stack((np.arctan2(d[..., 2], d[..., 1]) / (2 * np.pi) + 0.5,
                     np.arccos(np.clip(-d[..., 0], -1, 1)) / np.pi), axis=-1)


def scalp_field():
    audit = json.loads((ROOT / "Data/EthanV2/Groom/export.json").read_text())
    size = audit["sdf_resolution"]
    field = np.fromfile(GROOM / "EthanV2_HeadSDF.bytes", dtype="<f4").reshape([size] * 3)
    low, high = np.array(audit["sdf_min"]), np.array(audit["sdf_max"])

    def sample(points):
        grid = (points - low) / (high - low) * size - 0.5
        return ndimage.map_coordinates(field, grid.T[::-1], order=1, mode="nearest")

    return sample


def shell(curves, atlas, rng, sdf):
    roots = curves[:, 0]
    directions = unit(roots - CENTER)
    tree = cKDTree(directions)
    columns, rows = 132, 70
    u, v = np.meshgrid(np.linspace(0, 1, columns + 1), np.linspace(0.004, 0.88, rows))
    azimuth, polar = (u - 0.5) * 2 * np.pi, v * np.pi
    rays = np.stack((-np.cos(polar), np.sin(polar) * np.cos(azimuth),
                     np.sin(polar) * np.sin(azimuth)), axis=-1).reshape(-1, 3)
    distance, indices = tree.query(rays, k=12)
    radii = np.linalg.norm(roots - CENTER, axis=-1)
    weights = 1 / (distance + 0.012) ** 2
    radius = np.sum(radii[indices] * weights, axis=1) / weights.sum(axis=1)
    # Estimate the outer surface from groom coverage in each direction. A high
    # percentile tracks the body of the hairstyle without chasing individual flyaways.
    positions = curves[:, ::2].reshape(-1, 3) - CENTER
    groom_radii = np.linalg.norm(positions, axis=1)
    groom_tree = cKDTree(unit(positions))
    groom_distance, neighbors = groom_tree.query(rays, k=160, workers=-1)
    envelope = np.quantile(groom_radii[neighbors], 0.92, axis=1)
    smooth_envelope = ndimage.gaussian_filter(envelope.reshape(u.shape), 1.25, mode=("nearest", "wrap"))
    coverage = groom_distance[:, 95].reshape(u.shape)
    root_coverage = distance[:, 4].reshape(u.shape)
    footprint = ((coverage < 0.056) & (root_coverage < 0.083)) | (root_coverage < 0.046)
    footprint = ndimage.gaussian_filter(footprint.astype(float), 1.35, mode=("nearest", "wrap"))
    valid = (footprint > 0.075).ravel()
    # Keep the opaque surface under the outer cards and ease the rise at the hairline.
    edge_weight = np.clip((0.080 - root_coverage) / 0.045, 0.17, 1.0)
    rise = np.maximum(envelope - radius - 0.0014, 0).reshape(u.shape)
    rise = ndimage.gaussian_filter(np.minimum(rise, 0.050), (1.15, 1.15), mode=("nearest", "wrap"))
    points = CENTER + rays * (radius + 0.0014 + (0.94 * rise * edge_weight).ravel())[:, None]
    for _ in range(3):
        steps = np.eye(3) * 0.001
        gradient = np.stack([sdf(points + step) - sdf(points - step) for step in steps], axis=-1)
        points += unit(gradient) * np.clip(0.00055 - sdf(points), 0, 0.004)[:, None]

    faces = []
    for row in range(rows - 1):
        for column in range(columns):
            a = row * (columns + 1) + column
            b = a + columns + 1
            for triangle in ((a, a + 1, b), (a + 1, b + 1, b)):
                if np.all(valid[list(triangle)]):
                    faces.append(triangle)
    faces = np.asarray(faces)
    cross = np.cross(points[faces[:, 1]] - points[faces[:, 0]],
                     points[faces[:, 2]] - points[faces[:, 0]])
    if np.median(np.sum(cross * rays[faces[:, 0]], axis=1)) < 0:
        faces = faces[:, ::-1]

    # Project the groom's flow onto the fuller shell. Wrap at the rear seam.
    draw = ImageDraw.Draw(atlas)
    draw.rectangle((0, 0, ATLAS_SIZE - 1, SHELL_HEIGHT + 4), fill=(225, 223, 220, 255))
    sample = curves[rng.choice(len(curves), min(26000, len(curves)), replace=False), ::3]
    uv = spherical_uv(sample) * [ATLAS_SIZE - 1, SHELL_HEIGHT - 9] + [0, 4]
    for strand in uv:
        tone = int(rng.integers(199, 251))
        strand[:, 0] = np.unwrap(strand[:, 0] / (ATLAS_SIZE - 1) * 2 * np.pi) / (2 * np.pi) * (ATLAS_SIZE - 1)
        for offset in (-ATLAS_SIZE + 1, 0, ATLAS_SIZE - 1):
            if strand[:, 0].min() + offset < ATLAS_SIZE and strand[:, 0].max() + offset >= 0:
                draw.line(list(map(tuple, strand + [offset, 0])), fill=(tone, tone - 2, tone - 4, 255))
    # Use the cutout material on the narrow perimeter, with full depth-writing
    # coverage in the interior. Fine groom texture breaks up the alpha contour.
    height = SHELL_HEIGHT - 8
    polar_rows = np.linspace(0, 1, height)
    edge_y = (polar_rows - .004) / (.88 - .004) * (rows - 1)
    edge_x = np.linspace(0, columns, ATLAS_SIZE)
    grid_x, grid_y = np.meshgrid(edge_x, edge_y)
    coverage = ndimage.map_coordinates(footprint, [grid_y, grid_x], order=1, mode="constant")
    noise = ndimage.gaussian_filter(np.random.default_rng(702).random(coverage.shape), (0.6, 1.15)) - 0.5
    alpha = np.clip((coverage - 0.28 + 0.36 * noise) / 0.40, 0, 1)
    # Paste just the alpha channel. Keep the groom's RGB texture in the same region.
    channel = atlas.getchannel("A")
    channel.paste(Image.fromarray((255 * alpha).astype(np.uint8)), (0, 4))
    atlas.putalpha(channel)
    uv = np.stack((u.ravel(), 1 - (4 + v.ravel() * (SHELL_HEIGHT - 9)) / ATLAS_SIZE), axis=-1)
    rim = np.min(footprint.ravel()[faces], axis=1) < .82
    return ((points, rays, uv, faces[~rim]), (points, rays, uv, faces[rim]), smooth_envelope)


def group_curves(curves, rng):
    # Fit in curve space so strands that diverge from nearby roots get different cards.
    features = (curves[:, [0, 6, 13, 22, 31]] * np.array([1.3, 1, 1, 1, 1.1])[None, :, None]).reshape(-1, 15)
    centers = features[rng.choice(len(features), CARD_COUNT, replace=False)].copy()
    for _ in range(11):
        labels = cKDTree(centers).query(features)[1]
        counts = np.bincount(labels, minlength=CARD_COUNT)
        occupied = counts != 0
        for dimension in range(15):
            sums = np.bincount(labels, weights=features[:, dimension], minlength=CARD_COUNT)
            centers[occupied, dimension] = sums[occupied] / counts[occupied]
    order = np.argsort(labels, kind="stable")
    return np.split(order, np.cumsum(counts)[:-1])


def cards(curves, atlas, rng, envelope):
    vertices, normals, uv, faces = [], [], [], []
    indices = np.r_[0, 2, 5, 9, 14, 20, 25, 29, 31]
    width, height, gutter = 52, 88, 4
    for group in group_curves(curves, rng):
        if len(group) < 5:
            continue
        members = curves[group]
        guide = ndimage.gaussian_filter1d(np.median(members, axis=0), 0.8, axis=0)
        tangent = unit(np.gradient(guide, axis=0))
        radial = unit(guide - CENTER)
        across = unit(np.cross(tangent, radial))
        normal = unit(np.cross(across, tangent))
        offsets = np.sum((members - guide) * across, axis=-1)
        low, high = np.quantile(offsets, [0.04, 0.96], axis=0)
        low, high = ndimage.gaussian_filter1d(low, 1) - 0.0006, ndimage.gaussian_filter1d(high, 1) + 0.0006
        # Tighten the geometry itself at the tips, in addition to fading coverage in the atlas.
        taper = np.interp(np.arange(32), [0, 21, 28, 31], [1, 1, 0.82, 0.22])
        low, high = low * taper, high * taper
        # The original median guide removed the outer half of a clump's depth.
        # Follow its outer quartile above the roots, with a cap on sparse outliers.
        depth = np.sum((members - guide) * radial, axis=-1)
        lift = np.clip(ndimage.gaussian_filter1d(np.quantile(depth, 0.80, axis=0), 1.4), 0, 0.012)
        root_fade = np.interp(np.arange(32), [0, 4, 14], [0.12, 0.65, 1])
        center = guide + radial * (0.0006 + lift * root_fade)[:, None]
        left, right = center + across * low[:, None], center + across * high[:, None]
        edges = np.stack((left, right))
        angular_uv = spherical_uv(edges)
        row = (angular_uv[..., 1] - .004) / (.88 - .004) * (envelope.shape[0] - 1)
        column = angular_uv[..., 0] * (envelope.shape[1] - 1)
        limit = ndimage.map_coordinates(envelope, [row, column], order=1, mode="nearest") + 0.0034
        local = edges - CENTER
        radii = np.linalg.norm(local, axis=-1)
        # A sparse guide can arch well beyond the body of the groom. Pull just
        # those protruding tips inward to avoid visible detached ribbon loops.
        excess = ndimage.gaussian_filter1d(np.maximum(radii - limit, 0), 1.3, axis=-1)
        left, right = edges - unit(local) * (excess * root_fade)[..., None]

        tile_index = len(vertices) // (2 * len(indices))
        tile_x = tile_index % 34 * (width + 2 * gutter) + gutter
        tile_y = SHELL_HEIGHT + 8 + tile_index // 34 * (height + 2 * gutter) + gutter
        # Supersample each projected fiber, then downsample. Color extends through
        # zero-alpha texels so filtering cannot introduce a dark atlas fringe.
        tile = Image.new("RGBA", (width * 3, height * 3), (222, 220, 217, 0))
        draw = ImageDraw.Draw(tile)
        chosen = rng.choice(len(members), min(54, len(members)), replace=False)
        lane = (offsets[chosen] - low) / (high - low)
        for strand in lane:
            tone = int(rng.integers(195, 255))
            start = int(rng.integers(0, 3))
            end = int(rng.integers(23, 33))
            if guide[-1, 0] > -0.050 and guide[-1, 2] > 0.045:
                end = min(end, 29)
            x = (0.075 + 0.85 * strand) * (width * 3 - 1)
            for k in range(start, end - 1):
                edge = min(1, (strand[k] + 0.015) / 0.16, (1.015 - strand[k]) / 0.16)
                alpha = int(245 * max(edge, 0) * min(1, (k + 2) / 5, (end - k) / 8))
                y0, y1 = (height * 3 - 1) * np.array([k, k + 1]) / 31
                draw.line([(x[k], y0), (x[k + 1], y1)], fill=(tone, tone - 2, tone - 5, alpha),
                          width=4 if k < 23 else 3)
        tile = tile.resize((width, height), Image.Resampling.LANCZOS)
        rgba = np.array(tile)
        rgba[rgba[:, :, 3] == 0, :3] = (222, 220, 217)
        tile = Image.fromarray(rgba)
        atlas.paste(tile, (tile_x, tile_y))
        first = len(vertices)
        for k in indices:
            vertices.extend((left[k], right[k]))
            smooth_normal = unit(0.72 * radial[k] + 0.28 * normal[k])
            normals.extend((smooth_normal, smooth_normal))
            y = 1 - (tile_y + 0.5 + (height - 1) * k / 31) / ATLAS_SIZE
            uv.extend(((tile_x + 0.5, y), (tile_x + width - 0.5, y)))
        for k in range(first, len(vertices) - 2, 2):
            faces.extend(((k, k + 1, k + 2), (k + 1, k + 3, k + 2)))
    uv = np.asarray(uv)
    uv[:, 0] /= ATLAS_SIZE
    return np.asarray(vertices), np.asarray(normals), uv, np.asarray(faces)


def main():
    source = GROOM / "EthanV2_Scalp32.bytes"
    with source.open("rb") as stream:
        magic, count, samples = struct.unpack("<iii", stream.read(12))
        if magic != 0x48414952 or samples != 32:
            raise ValueError("Expected Ethan's 32-point fitted groom")
        curves = np.fromfile(stream, dtype="<f4").reshape(count, samples, 3)
    lengths = np.linalg.norm(np.diff(curves, axis=1), axis=2).sum(axis=1)
    curves = curves[lengths >= 0.004]
    rng = np.random.default_rng(2204)
    atlas = Image.new("RGBA", (ATLAS_SIZE, ATLAS_SIZE), (222, 220, 217, 0))
    sdf = scalp_field()
    core, rim, envelope = shell(curves, atlas, rng, sdf)
    outer = cards(curves, atlas, rng, envelope)
    core, rim = (compact(core), compact(rim))
    clipped = tuple(np.concatenate((a, b)) for a, b in zip(rim[:3], outer[:3]))
    clipped += (np.concatenate((rim[3], outer[3] + len(rim[0]))),)
    parts = tuple(fit_silhouette(part, sdf) for part in (core, clipped))
    triangles = sum(len(part[3]) for part in parts)
    if triangles > 20000:
        raise ValueError(f"{triangles} triangles exceeds the prototype budget")
    OUTPUT.mkdir(parents=True, exist_ok=True)
    atlas.save(OUTPUT / "EthanGroomAtlas.png", optimize=True)
    binary = ROOT / "Temp/EthanHairComparison.mesh"
    binary.parent.mkdir(exist_ok=True)
    with binary.open("wb") as stream:
        for vertices, normals, uv, faces in parts:
            stream.write(struct.pack("<ii", len(vertices), faces.size))
            np.column_stack((vertices, normals, uv)).astype("<f4").tofile(stream)
            faces.astype("<i4").tofile(stream)
    print(json.dumps({
        "sourceStrands": count,
        "usableStrands": len(curves),
        "triangles": [len(part[3]) for part in parts],
        "vertices": [len(part[0]) for part in parts],
        "shellPointClearanceMm": (np.quantile(sdf(parts[0][0]), [0, 0.05, 0.5]) * 1000).tolist(),
    }))


def compact(part):
    vertices, normals, uv, faces = part
    used, indices = np.unique(faces, return_inverse=True)
    return vertices[used], normals[used], uv[used], indices.reshape(-1, 3)


def fit_silhouette(part, sdf):
    vertices, normals, uv, faces = part
    local = vertices - CENTER

    def smooth(t):
        t = np.clip(t, 0, 1)
        return t * t * (3 - 2 * t)

    # Correct the remaining shortfall measured in the front and profile plates.
    # Blend away from the fitted scalp to keep the part and contact edge anchored.
    lift = smooth((-local[:, 0] - 0.027) / 0.062)
    clearance = smooth((sdf(vertices) - 0.0015) / 0.012)
    width = np.interp(local[:, 1], [-0.083, 0, 0.09], [0.043, 0.053, 0.103])
    nape = smooth((local[:, 2] - 0.026) / 0.055) * smooth((local[:, 0] - 0.020) / 0.064)
    displacement = np.stack((-0.0098 * lift, (width - 0.10 * nape) * local[:, 1],
                             0.047 * local[:, 2]), axis=1)
    return vertices + displacement * clearance[:, None], normals, uv, faces


if __name__ == "__main__":
    main()
