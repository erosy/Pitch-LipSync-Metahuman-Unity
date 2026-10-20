"""Export Batch 3 files outside Assets; preserve the prepared .blend."""
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Data/JiwooReimport/Batch03'


def export_hair_abc(sampled):
    count, resolution, _ = sampled.shape
    # The isolated Unity FBX import mirrors head-local Z. Alembic swaps X.
    # Bake a 180-degree Y rotation into the ABC coordinates so both importers
    # produce the same head frame. Also compensate ABC's automatic Z-up -> Y-up.
    abc_points = sampled.reshape(-1, 3)[:, [0, 2, 1]].copy()
    abc_points[:, 0] *= -1
    data = bpy.data.hair_curves.new('Jiwoo_Bob_ExportCurves')
    data.add_curves([resolution] * count)
    data.attributes['position'].data.foreach_set('vector', abc_points.ravel())
    radius = data.attributes.new('radius', 'FLOAT', 'POINT')
    radius.data.foreach_set('value', np.full(count*resolution, 0.00005, dtype=np.float32))
    export_hair = bpy.data.objects.new('Jiwoo_Bob_HeadLocal', data)
    bpy.context.scene.collection.objects.link(export_hair)
    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    export_hair.select_set(True)
    bpy.context.view_layer.objects.active = export_hair
    bpy.context.view_layer.update()
    bpy.ops.wm.alembic_export(filepath=str(OUT / 'Jiwoo_Bob_HeadLocal.abc'),
                              start=1, end=1, selected=True, flatten=True,
                              global_scale=1, curves_as_mesh=False, export_hair=True,
                              export_particles=False, export_custom_properties=False,
                              as_background_job=False, init_scene_frame_range=False)


def array(data, prop, n=3):
    a = np.empty(len(data) * n, dtype=np.float32)
    data.foreach_get(prop, a)
    return a.reshape(-1, n) if n > 1 else a


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    rig, face, hair = [bpy.data.objects[n] for n in ('Jiwoo_Armature', 'Face', 'Hair')]
    source_hash = hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest()
    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    rig.select_set(True)
    face.select_set(True)
    bpy.context.view_layer.objects.active = rig
    fbx_settings = dict(use_selection=True, object_types={'ARMATURE', 'MESH'},
                        global_scale=1.0, apply_unit_scale=True,
                        apply_scale_options='FBX_SCALE_UNITS',
                        axis_forward='-Z', axis_up='Y', use_space_transform=True,
                        bake_space_transform=False, use_mesh_modifiers=False,
                        add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
                        use_armature_deform_only=False, bake_anim=False,
                        mesh_smooth_type='FACE', path_mode='AUTO', embed_textures=False,
                        use_custom_props=False)
    bpy.ops.export_scene.fbx(filepath=str(OUT / 'Jiwoo_Normalized.fbx'), **fbx_settings)

    # Preserve baselines for independent import tests.
    mesh = array(face.data.vertices, 'co')
    shapes = {key.name: array(key.data, 'co') for key in face.data.shape_keys.key_blocks}
    np.savez_compressed(OUT / 'character_baseline.npz', mesh=mesh, **shapes)
    bone_origins = {bone.name: list((rig.matrix_world @ bone.matrix_local).translation)
                    for bone in rig.data.bones}
    head_matrix = np.array(rig.matrix_world @ rig.pose.bones['head'].matrix)
    positions = array(hair.data.attributes['position'].data, 'vector')
    left = array(hair.data.attributes['handle_left'].data, 'vector')
    right = array(hair.data.attributes['handle_right'].data, 'vector')
    offsets = np.empty(len(hair.data.curve_offset_data), dtype=np.int32)
    hair.data.curve_offset_data.foreach_get('value', offsets)
    count = len(hair.data.curves)
    resolution = 64
    sampled = np.empty((count, resolution, 3), dtype=np.float32)
    lengths = np.empty(count)
    deviations = np.empty(count)
    t = np.linspace(0, 1, 25)[:-1][None, :, None]
    for i in range(count):
        a, b = offsets[i:i+2]
        p0 = positions[a:b-1, None, :]
        p1 = right[a:b-1, None, :]
        p2 = left[a+1:b, None, :]
        p3 = positions[a+1:b, None, :]
        dense = ((1-t)**3*p0 + 3*(1-t)**2*t*p1 + 3*(1-t)*t**2*p2 + t**3*p3).reshape(-1, 3)
        dense = np.concatenate((dense, positions[b-1:b]), axis=0)
        cumulative = np.r_[0, np.cumsum(np.linalg.norm(np.diff(dense, axis=0), axis=1))]
        lengths[i] = cumulative[-1]
        targets = np.linspace(0, cumulative[-1], resolution)
        sampled[i] = np.column_stack([np.interp(targets, cumulative, dense[:,j]) for j in range(3)])
        # Compare dense Bezier samples with the corresponding resampled chord.
        indices = np.clip(np.searchsorted(targets, cumulative, side='right')-1, 0, resolution-2)
        start = sampled[i, indices]
        delta = sampled[i, indices+1] - start
        u = np.clip(np.sum((dense-start)*delta, axis=1) /
                    np.maximum(np.sum(delta*delta, axis=1), 1e-20), 0, 1)
        deviations[i] = np.max(np.linalg.norm(dense-(start+u[:,None]*delta), axis=1))
        if i % 20000 == 0:
            print('RESAMPLE', i, '/', count, flush=True)
    np.save(OUT / 'hair_head_local_baseline.npy', sampled)

    export_hair_abc(sampled)

    # Export only material images referenced by the combined character.
    textures = OUT / 'Textures'
    textures.mkdir(exist_ok=True)
    saved_images = set()
    for material in face.data.materials:
        if material:
            for node in material.node_tree.nodes if material.node_tree else []:
                if node.type == 'TEX_IMAGE' and node.image and node.image.name not in saved_images:
                    image = node.image
                    if image.packed_file:
                        image.filepath_raw = str(textures / Path(image.filepath).name)
                        image.save()
                        saved_images.add(image.name)

    report = {
        'source_prepared_blend': bpy.data.filepath, 'prepared_sha256': source_hash,
        'fbx_settings': {k: sorted(v) if isinstance(v, set) else v for k,v in fbx_settings.items()},
        'bones': bone_origins, 'head_matrix_blender': head_matrix.tolist(),
        'shape_names': list(shapes)[1:], 'material_names': [m.name for m in face.data.materials],
        'curve_count': count, 'points_per_curve': resolution,
        'strand_length_max_m': float(lengths.max()), 'strand_length_average_m': float(lengths.mean()),
        'resample_error_max_m': float(deviations.max()),
        'resample_error_p99_m': float(np.percentile(deviations, 99)),
        'strand_diameter_m': 0.0001,
        'abc_head_axis_correction': [-1, 1, -1],
        'hair_local_bounds': [sampled.min(axis=(0,1)).tolist(), sampled.max(axis=(0,1)).tolist()],
        'texture_count': len(saved_images), 'status': 'EXPORTED; verification pending',
    }
    (OUT / 'export_report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    assert hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest() == source_hash
    print('JIWOO_EXPORT ' + json.dumps({k: report[k] for k in ['curve_count', 'strand_length_max_m', 'resample_error_max_m', 'texture_count']}), flush=True)


if '--hair-only' in sys.argv:
    export_hair_abc(np.load(OUT / 'hair_head_local_baseline.npy'))
    report = json.loads((OUT / 'export_report.json').read_text(encoding='utf-8'))
    report['abc_head_axis_correction'] = [-1, 1, -1]
    (OUT / 'export_report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
else:
    main()
