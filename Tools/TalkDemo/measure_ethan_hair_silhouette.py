"""Measure matched Edit Mode captures after EthanHairComparisonEditor.Review."""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
REVIEW = ROOT / "Docs/EthanHairComparison"
HIDDEN = ROOT / "Temp/EthanHairComparison"
ANGLES = [0, 45, 85]
ROWS = [("Groom", "EthanV2_ScalpHair"), ("Cards", "Groom-baked cards"),
        ("Ribbons", "Current web fallback ribbons")]


def pixels(path):
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)


def visible_hair(image, hidden):
    # Light backdrop isolates even dark, antialiased hair. Requiring a color change
    # against the hidden plate removes the unchanged eyebrows, skin and background.
    difference = np.max(np.abs(image - hidden), axis=-1)
    luminance = image @ [0.2126, 0.7152, 0.0722]
    weight = np.clip((difference - 12) / 48, 0, 1) * np.clip((145 - luminance) / 64, 0, 1)
    weight[:85] = 0
    weight[236:] = 0
    weight[:, :95] = 0
    weight[:, 270:] = 0
    mask = ndimage.gaussian_filter(weight, 1.05) > 0.29
    labels, _ = ndimage.label(mask)
    counts = np.bincount(labels.ravel())
    counts[0] = 0
    if not np.any(counts):
        raise ValueError("Hair is missing from the capture")
    return labels == counts.argmax()


def bounds(mask):
    y, x = np.where(mask)
    return {"left": int(x.min()), "right": int(x.max()), "top": int(y.min()),
            "bottom": int(y.max()), "width": int(np.ptp(x) + 1), "height": int(np.ptp(y) + 1)}


def compare(candidate, reference):
    candidate_edge = candidate & ~ndimage.binary_erosion(candidate)
    reference_edge = reference & ~ndimage.binary_erosion(reference)
    offset = np.concatenate((
        ndimage.distance_transform_edt(~reference_edge)[candidate_edge],
        ndimage.distance_transform_edt(~candidate_edge)[reference_edge],
    ))
    return {"boundsPx": bounds(candidate), "coveragePercent": round(100 * candidate.sum() / reference.sum(), 1),
            "silhouetteIoU": round(float((candidate & reference).sum() / (candidate | reference).sum()), 3),
            "meanOutlineDistancePx": round(float(offset.mean()), 2)}


def contact_sheets():
    for background in ["light", "dark"]:
        image = Image.new("RGB", (1080, 1632), (32, 36, 45))
        draw = ImageDraw.Draw(image)
        for row, (mode, label) in enumerate(ROWS):
            for column, angle in enumerate(ANGLES):
                x, y = column * 360, row * 544
                with Image.open(REVIEW / f"{mode}-{angle}-{background}.png") as shot:
                    image.paste(shot, (x, y + 24))
                draw.text((x + 7, y + 6), f"{label} / {angle} degrees", fill="white")
        image.save(REVIEW / f"comparison-{background}.png")


def front_comparison(previous):
    sources = [(REVIEW, "Groom", "Actual groom")]
    if previous:
        sources.append((previous, "Cards", "Previous cards"))
    sources.append((REVIEW, "Cards", "Revised cards"))
    image = Image.new("RGB", (300 * len(sources), 351), (32, 36, 45))
    draw = ImageDraw.Draw(image)
    for column, (folder, mode, label) in enumerate(sources):
        with Image.open(folder / f"{mode}-0-light.png") as shot:
            head = shot.crop((106, 90, 256, 253)).resize((300, 326), Image.Resampling.LANCZOS)
        image.paste(head, (column * 300, 25))
        draw.text((column * 300 + 8, 7), label, fill="white")
    image.save(REVIEW / "front-shape.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--previous", type=Path, help="Folder with the pre-adjustment Cards captures")
    previous = parser.parse_args().previous
    results = {}
    for angle in ANGLES:
        hidden = pixels(HIDDEN / f"Hidden-{angle}-light.png")
        reference = visible_hair(pixels(REVIEW / f"Groom-{angle}-light.png"), hidden)
        cards = visible_hair(pixels(REVIEW / f"Cards-{angle}-light.png"), hidden)
        result = {"referenceBoundsPx": bounds(reference), "cards": compare(cards, reference)}
        if previous:
            before = visible_hair(pixels(previous / f"Cards-{angle}-light.png"), hidden)
            result["previousCards"] = compare(before, reference)
        results[angle] = result
    (REVIEW / "silhouette.json").write_text(json.dumps(results, indent=2) + "\n")
    contact_sheets()
    front_comparison(previous)
    print(json.dumps(results))


if __name__ == "__main__":
    main()
