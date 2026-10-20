"""Batch 2: prepare a separate Blender export scene, then verify it on reopen.

Run using Blender 5.1 --background --factory-startup --disable-autoexec SOURCE
--python this_file.py -- prepare (or PREPARED ... -- verify).
All generated files stay outside Unity's Assets directory.
"""
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector


ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Data' / 'JiwooReimport' / 'Batch02'
PREPARED = OUT / 'Jiwoo_ExportPrepared.blend'
BASELINE = OUT / 'preparation_report.json'
HAIR_RADIUS_M = 0.00005  # 0.1 mm diameter; stored as radius in Blender.
MAX_ERROR_M = 0.000001


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def digest_file(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def vector_array(collection, field='co'):
    result = np.empty(len(collection) * 3, dtype=np.float32)
    collection.foreach_get(field, result)
    return result.reshape(-1, 3)


def transformed(points, matrix):
    m = np.array(matrix, dtype=np.float64)
    return points.astype(np.float64) @ m[:3, :3].T + m[:3, 3]


def error(a, b):
    require(a.shape == b.shape, 'Geometry count changed.')
    return float(np.max(np.linalg.norm(a - b, axis=1)))


def mesh_world(mesh):
    evaluated = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    data = evaluated.to_mesh()
    try:
        return transformed(vector_array(data.vertices), evaluated.matrix_world)
    finally:
        evaluated.to_mesh_clear()


def character_signature(mesh, rig):
    keys = mesh.data.shape_keys
    require(keys is not None, 'Export mesh has no shape keys.')
    shapes = {key.name: hashlib.sha256(vector_array(key.data).tobytes()).hexdigest()
              for key in keys.key_blocks}
    weights = hashlib.sha256()
    for vertex in mesh.data.vertices:
        for group in vertex.groups:
            weights.update(np.array([vertex.index, group.group], dtype=np.int32).tobytes())
            weights.update(np.float32(group.weight).tobytes())
    return {
        'vertices': len(mesh.data.vertices),
        'polygons': len(mesh.data.polygons),
        'shape_keys_sha256': shapes,
        'shape_values': {k.name: float(k.value) for k in keys.key_blocks},
        'vertex_group_names': [g.name for g in mesh.vertex_groups],
        'weights_sha256': weights.hexdigest(),
        'materials': [m.name if m else None for m in mesh.data.materials],
        'bones': [{'name': b.name, 'parent': b.parent.name if b.parent else None,
                   'matrix': [list(row) for row in b.matrix_local],
                   'use_deform': b.use_deform} for b in rig.data.bones],
    }


def hair_world(hair):
    return {name: transformed(vector_array(hair.data.attributes[name].data, 'vector'),
                              hair.matrix_world)
            for name in ('position', 'handle_left', 'handle_right')}


def check_neutral(mesh, rig):
    require(not rig.constraints, 'Export rig has object constraints.')
    require(not rig.animation_data or not rig.animation_data.drivers, 'Rig has drivers.')
    for bone in rig.pose.bones:
        require(not bone.constraints, 'Export bone has constraints: ' + bone.name)
        require(np.max(np.abs(np.array(bone.matrix_basis) - np.eye(4))) < 1e-5,
                'Export rig is posed: ' + bone.name)
    require(not mesh.data.shape_keys.animation_data or
            not mesh.data.shape_keys.animation_data.drivers, 'Shape keys have drivers.')
    require(all(abs(k.value) < 1e-8 for k in list(mesh.data.shape_keys.key_blocks)[1:]),
            'Character is not in a neutral facial pose.')


def prepare():
    require(not PREPARED.exists(), 'Prepared file already exists; refusing to overwrite it.')
    source = Path(bpy.data.filepath)
    require(source.name == 'Jiwoon Eros Try - Fixed v3.blend', 'Unexpected source file.')
    OUT.mkdir(parents=True, exist_ok=True)
    rig = bpy.data.objects['Jiwoo_Armature']
    mesh = bpy.data.objects['Face']
    hair = bpy.data.objects['Hair']
    check_neutral(mesh, rig)
    signature = character_signature(mesh, rig)
    before_mesh = mesh_world(mesh)
    before_hair = hair_world(hair)
    head_world = rig.matrix_world @ rig.pose.bones['head'].matrix
    original_hair_world = hair.matrix_world.copy()
    original_parent = hair.parent.name
    source_hash = digest_file(source)

    # Store positions AND Bezier handles in the export head's local meter space.
    conversion = head_world.inverted() @ original_hair_world
    for name in before_hair:
        attribute = hair.data.attributes[name]
        positions = vector_array(attribute.data, 'vector')
        positions = transformed(positions, conversion).astype(np.float32)
        attribute.data.foreach_set('vector', positions.ravel())
    radius = hair.data.attributes.get('radius')
    if radius is None:
        radius = hair.data.attributes.new('radius', 'FLOAT', 'POINT')
    require(radius.domain == 'POINT' and radius.data_type == 'FLOAT',
            'Unexpected existing radius attribute.')
    radius.data.foreach_set('value', np.full(len(hair.data.points), HAIR_RADIUS_M,
                                            dtype=np.float32))

    # Blender bone parenting uses the tail frame. Compensate once in the parent
    # inverse so Hair's basis stays identity and its world frame is the head frame.
    hair.parent = rig
    hair.parent_type = 'BONE'
    hair.parent_bone = 'head'
    hair.matrix_parent_inverse = Matrix.Translation((0, -rig.data.bones['head'].length, 0))
    hair.matrix_basis = Matrix.Identity(4)
    hair.data.update_tag()
    bpy.context.view_layer.update()
    require(np.max(np.abs(np.array(hair.matrix_world) - np.array(head_world))) < 1e-5,
            'Bone parenting did not produce the expected head frame.')

    # Clean only the in-memory export copy. Keep data blocks/materials to avoid
    # changing the character; the original source is never saved.
    removed = []
    for obj in list(bpy.data.objects):
        if obj not in (rig, mesh, hair):
            removed.append(obj.name)
            bpy.data.objects.remove(obj, do_unlink=True)
    for text in list(bpy.data.texts):
        bpy.data.texts.remove(text)
    for collection in list(bpy.data.collections):
        if not collection.objects and not collection.children:
            bpy.data.collections.remove(collection)

    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = 'METERS'
    scene.frame_start = scene.frame_end = scene.frame_current = 1
    hair['groom_coordinate_space'] = 'Jiwoo_Armature/head local, meters, Blender bone axes'
    hair['strand_diameter_m'] = HAIR_RADIUS_M * 2
    hair['export_note'] = 'Export hair separately; verify ABC transforms and widths in Batch 3.'
    rig['export_role'] = 'Character FBX: select Jiwoo_Armature and Face only.'
    scene['source_blend'] = str(source)
    scene['source_sha256'] = source_hash
    scene['batch'] = 'Batch 2 prepared scene; export/import verification pending Batch 3.'

    # Keep usable texture paths valid after Save As moves the .blend directory.
    for image in bpy.data.images:
        if image.filepath:
            image.filepath = bpy.path.abspath(image.filepath)
    packed = []
    for material in mesh.data.materials:
        if material and material.use_nodes:
            for node in material.node_tree.nodes:
                if node.type == 'TEX_IMAGE' and node.image:
                    image = node.image
                    if image.filepath and Path(image.filepath).is_file() and not image.packed_file:
                        image.pack()
                        packed.append(image.name)

    bpy.context.view_layer.update()
    require(character_signature(mesh, rig) == signature,
            'Character shapes, weights, materials, or skeleton changed.')
    mesh_error = error(before_mesh, mesh_world(mesh))
    after_hair = hair_world(hair)
    hair_errors = {name: error(before_hair[name], after_hair[name]) for name in before_hair}
    require(mesh_error <= MAX_ERROR_M and max(hair_errors.values()) <= MAX_ERROR_M,
            'Preparation changed the world-space character or groom fit.')

    # Baseline arrays support an independent process reopening the saved file.
    np.savez_compressed(OUT / 'geometry_baseline.npz', mesh=before_mesh,
                        **before_hair)
    report = {
        'source': str(source), 'source_sha256': source_hash,
        'prepared': str(PREPARED), 'blender': bpy.app.version_string,
        'original_hair_parent': original_parent,
        'prepared_hair_parent': rig.name + '/head',
        'character_signature': signature,
        'curve_count': len(hair.data.curves), 'point_count': len(hair.data.points),
        'hair_radius_m': HAIR_RADIUS_M, 'hair_diameter_mm': HAIR_RADIUS_M * 2000,
        'mesh_world_error_m': mesh_error, 'hair_world_errors_m': hair_errors,
        'removed_objects_from_copy': removed, 'packed_images': sorted(set(packed)),
        'head_world_matrix': [list(row) for row in head_world],
        'remaining_objects': [o.name for o in scene.objects],
        'status': 'Prepared; independent reopen verification pending',
    }
    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    mesh.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.wm.save_as_mainfile(filepath=str(PREPARED), check_existing=False,
                               relative_remap=False)
    require(digest_file(source) == source_hash, 'Original source hash changed.')
    report['prepared_sha256'] = digest_file(PREPARED)
    BASELINE.write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('JIWOO_PREPARED ' + str(PREPARED), flush=True)
    print('WORLD_ERRORS ' + json.dumps({'mesh_m': mesh_error, **hair_errors}), flush=True)


def verify():
    report = json.loads(BASELINE.read_text(encoding='utf-8'))
    require(Path(bpy.data.filepath).resolve() == PREPARED, 'Open the prepared file to verify.')
    mesh, rig, hair = [bpy.data.objects[n] for n in ('Face', 'Jiwoo_Armature', 'Hair')]
    check_neutral(mesh, rig)
    require(character_signature(mesh, rig) == report['character_signature'],
            'Saved character data differs from the source signature.')
    require(set(o.name for o in bpy.context.scene.objects) == {'Face', 'Jiwoo_Armature', 'Hair'},
            'Unexpected authoring objects remain.')
    require(not bpy.data.texts, 'Embedded scripts remain in the prepared file.')
    require(len(hair.data.curves) == report['curve_count'], 'Curve count changed.')
    require(len(hair.data.points) == report['point_count'], 'Control-point count changed.')
    require(hair.parent == rig and hair.parent_bone == 'head', 'Wrong hair attachment.')
    baseline = np.load(OUT / 'geometry_baseline.npz')
    errors = {'mesh': error(baseline['mesh'], mesh_world(mesh))}
    errors.update({name: error(baseline[name], points) for name, points in hair_world(hair).items()})
    require(max(errors.values()) <= MAX_ERROR_M, 'Saved geometry exceeded 1 micrometer tolerance.')
    radius = np.empty(len(hair.data.points), dtype=np.float32)
    hair.data.attributes['radius'].data.foreach_get('value', radius)
    require(np.allclose(radius, HAIR_RADIUS_M, rtol=1e-6, atol=0), 'Saved radius changed.')

    # Prove rigid head motion carries every curve control point and both handles.
    rest_world = hair_world(hair)
    head = rig.pose.bones['head']
    rest_basis = head.matrix_basis.copy()
    rest_head = rig.matrix_world @ head.matrix
    head.matrix_basis = Matrix.Rotation(np.deg2rad(15), 4, 'Y')
    bpy.context.view_layer.update()
    posed_head = rig.matrix_world @ head.matrix
    rigid_delta = posed_head @ rest_head.inverted()
    motion_errors = {name: error(transformed(points, rigid_delta), hair_world(hair)[name])
                     for name, points in rest_world.items()}
    head.matrix_basis = rest_basis
    bpy.context.view_layer.update()
    require(max(motion_errors.values()) <= MAX_ERROR_M, 'Hair does not follow the export head.')
    require(digest_file(report['source']) == report['source_sha256'], 'Original source changed.')
    result = {
        'status': 'PASS', 'reopened_blend': str(PREPARED),
        'world_geometry_errors_m': errors, 'head_rotation_follow_errors_m': motion_errors,
        'tolerance_m': MAX_ERROR_M, 'shape_key_count_including_basis': len(mesh.data.shape_keys.key_blocks),
        'bone_count': len(rig.data.bones), 'curve_count': len(hair.data.curves),
        'point_count': len(hair.data.points), 'diameter_mm': HAIR_RADIUS_M * 2000,
        'source_unchanged': True, 'unity_import_ready': False,
        'next_batch': 'FBX/ABC exports, scale/axes/width and shape-key round-trip checks',
    }
    (OUT / 'verification_report.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print('JIWOO_VERIFICATION ' + json.dumps(result), flush=True)


if __name__ == '__main__':
    mode = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else ''
    require(mode in {'prepare', 'verify'}, 'Specify -- prepare or -- verify')
    if mode == 'prepare':
        prepare()
    else:
        verify()
