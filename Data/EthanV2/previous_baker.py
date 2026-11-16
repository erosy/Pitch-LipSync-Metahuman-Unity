"""Bake rig-authored facial poses, without executing the delivery's embedded scripts.

blender --background --factory-startup --disable-autoexec Jiwoo_Rigged.blend
  --python Tools/Hair/bake_jiwoo_rigged_shapes.py
Reopen the resulting file with the same flags and add -- --verify to verify it.
Only Data/JiwooRigged/Batch01 is written. Unity assets are never touched.
"""
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Data/JiwooRigged/Batch01'
PREPARED = OUT / 'Jiwoo_Rigged_Baked.blend'
PREFIX = 'Jiwoo_MetaHumanCharacter_'
TOLERANCE = 2e-6  # meters, evaluated-space comparison


def digest(path):
    with Path(path).open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


def coords(data):
    result = np.empty(len(data) * 3, dtype=np.float32)
    data.foreach_get('co', result)
    return result.reshape(-1, 3).astype(np.float64)


def evaluated(obj):
    ev = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = ev.to_mesh()
    try:
        assert len(mesh.vertices) == len(obj.data.vertices), obj.name + ': changed topology'
        return coords(mesh.vertices)
    finally:
        ev.to_mesh_clear()


def update():
    bpy.context.view_layer.update()


def distance_m(obj, a, b):
    linear = np.array(obj.matrix_world.to_3x3(), dtype=np.float64)
    return float(np.max(np.linalg.norm((a - b) @ linear.T, axis=1)))


def poses():
    """Translations in rig-space meters; rotations in rig-space radians."""
    p = {}

    def put(name, *ops):
        p[name] = list(ops)

    def t(bone, x=0, y=0, z=0):
        return {'bone': bone, 'translation_m': [x, y, z]}

    def jaw(angle=0, x=0, y=0):
        return {'bone': 'jaw_master', 'translation_m': [x, y, 0], 'rotation_xyz_rad': [angle, 0, 0]}

    def lids(side, top, bottom):
        return [t('lid.' + part + '.' + side + '.002', z=value)
                for part, value in [('T', top), ('B', bottom)]]

    def corners(x=0, y=0, z=0):
        return [t('lips.L', x=x, y=y, z=z), t('lips.R', x=-x, y=y, z=z)]

    for side, word, sign in [('L', 'Left', 1), ('R', 'Right', -1)]:
        put('eyeBlink' + word, *lids(side, -.008, .004))
        put('eyeWide' + word, *lids(side, .005, -.003))
        put('eyeSquint' + word, *lids(side, -.003, .003), t('cheek.T.' + side + '.001', z=.003))
        for direction, x, z in [('Up', 0, .08), ('Down', 0, -.08),
                                ('In', -sign * .08, 0), ('Out', sign * .08, 0)]:
            put('eyeLook' + direction + word, t('eye.' + side, x=x, z=z))
        put('browDown' + word, t('brow.T.' + side + '.001', z=-.006),
            t('brow.T.' + side + '.003', x=-sign * .002, z=-.006))
        put('browOuterUp' + word, t('brow.T.' + side + '.001', z=.009))
        put('cheekSquint' + word, t('cheek.T.' + side + '.001', z=.006), *lids(side, 0, .002))
        put('noseSneer' + word, t('nose.' + side + '.001', z=.005), t('lip.T.' + side + '.001', z=.003))
        put('mouthSmile' + word, t('lips.' + side, x=sign * .007, z=.006))
        put('mouthFrown' + word, t('lips.' + side, z=-.006))
        put('mouthDimple' + word, t('lips.' + side, x=sign * .004, y=.004))
        put('mouthStretch' + word, t('lips.' + side, x=sign * .010))
        put('mouthPress' + word, t('lip.T.' + side + '.001', z=-.002),
            t('lip.B.' + side + '.001', z=.003))
        put('mouthLowerDown' + word, t('lip.B.' + side + '.001', z=-.007))
        put('mouthUpperUp' + word, t('lip.T.' + side + '.001', z=.006))
    put('browInnerUp', t('brow.T.L.003', z=.009), t('brow.T.R.003', z=.009))
    put('cheekPuff', t('cheek.B.L.001', x=.005, y=-.008), t('cheek.B.R.001', x=-.005, y=-.008))
    put('jawOpen', jaw(.32))
    put('jawForward', jaw(y=-.008))
    put('jawLeft', jaw(x=.007))
    put('jawRight', jaw(x=-.007))
    put('mouthClose', t('lip.T', z=-.004), t('lip.B', z=.006))
    put('mouthFunnel', jaw(.12), *corners(x=-.006, y=-.006), t('lip.T', y=-.008, z=.002), t('lip.B', y=-.008, z=-.002))
    put('mouthPucker', *corners(x=-.008, y=-.009), t('lip.T', y=-.012, z=-.002), t('lip.B', y=-.012, z=.003))
    put('mouthLeft', t('lip.T', x=.007), t('lip.B', x=.007), t('lips.L', x=.007), t('lips.R', x=.007))
    put('mouthRight', t('lip.T', x=-.007), t('lip.B', x=-.007), t('lips.L', x=-.007), t('lips.R', x=-.007))
    put('mouthRollLower', t('lip.B', y=.006, z=.002))
    put('mouthRollUpper', t('lip.T', y=.006, z=-.002))
    put('mouthShrugLower', t('lip.B', z=.006), t('chin.001', z=.004))
    put('mouthShrugUpper', t('lip.T', z=.005), t('nose.004', z=.002))
    put('tongueOut', t('tongue_master', y=-.025), jaw(.10))
    put('viseme_sil')  # Intentional neutral silence, not a missing-expression placeholder.
    put('viseme_PP', t('lip.T', z=-.004, y=-.002), t('lip.B', z=.005, y=-.002))
    put('viseme_FF', jaw(.05), t('lip.B', y=.004, z=.004), t('lip.T', z=.002))
    put('viseme_TH', jaw(.12), t('tongue_master', y=-.014, z=.003))
    put('viseme_DD', jaw(.16), t('tongue', z=.009, y=-.004))
    put('viseme_kk', jaw(.20), t('tongue.002', z=.008))
    put('viseme_CH', jaw(.10), *corners(x=-.004, y=-.004), t('lip.T', y=-.004))
    put('viseme_SS', jaw(.035), *corners(x=.004), t('lip.T', z=.002))
    put('viseme_nn', jaw(.09), t('tongue', z=.006))
    put('viseme_RR', jaw(.14), *corners(x=-.003), t('tongue', z=.004, y=.004))
    put('viseme_aa', jaw(.29), t('lip.B', z=-.003))
    put('viseme_E', jaw(.15), *corners(x=.006, z=.002))
    put('viseme_I', jaw(.07), *corners(x=.008, z=.002))
    put('viseme_O', jaw(.23), *corners(x=-.006, y=-.005), t('lip.T', y=-.005))
    put('viseme_U', jaw(.07), *corners(x=-.008, y=-.009), t('lip.T', y=-.009), t('lip.B', y=-.008))
    combos = {
        'emo_joy': ['mouthSmileLeft', 'mouthSmileRight', 'cheekSquintLeft', 'cheekSquintRight'],
        'emo_sadness': ['mouthFrownLeft', 'mouthFrownRight', 'browInnerUp'],
        'emo_anger': ['browDownLeft', 'browDownRight', 'mouthPressLeft', 'mouthPressRight'],
        'emo_surprise': ['browInnerUp', 'browOuterUpLeft', 'browOuterUpRight', 'eyeWideLeft', 'eyeWideRight', 'jawOpen'],
        'emo_fear': ['browInnerUp', 'eyeWideLeft', 'eyeWideRight', 'mouthStretchLeft', 'mouthStretchRight'],
        'emo_disgust': ['noseSneerLeft', 'noseSneerRight', 'mouthUpperUpLeft', 'mouthUpperUpRight'],
    }
    for name, parts in combos.items():
        p[name] = [op for part in parts for op in p[part]]
    return p


def weights_signature(obj):
    h = hashlib.sha256()
    for v in obj.data.vertices:
        for g in v.groups:
            h.update(np.array([v.index, g.group], dtype=np.int32).tobytes())
            h.update(np.float32(g.weight).tobytes())
    return h.hexdigest()


def verify():
    report = json.loads((OUT / 'bake_report.json').read_text(encoding='utf-8-sig'))
    expected = np.load(OUT / 'evaluated_baselines.npz')
    errors = {}
    for item in report['meshes']:
        obj = bpy.data.objects[item['baked']]
        keys = obj.data.shape_keys
        assert weights_signature(obj) == item['weights_sha256']
        assert len(obj.data.vertices) == item['vertices']
        assert [k.name for k in keys.key_blocks] == ['Basis'] + report['shape_names']
        assert all(k.value == 0 for k in keys.key_blocks)
        neutral = evaluated(obj)
        assert distance_m(obj, neutral, expected[item['baked'] + '|Basis']) < TOLERANCE
        for name in report['shape_names']:
            key = keys.key_blocks[name]
            key.value = 1
            update()
            err = distance_m(obj, evaluated(obj), expected[item['baked'] + '|' + name])
            errors[item['baked'] + '|' + name] = err
            assert err < TOLERANCE, (obj.name, name, err)
            key.value = 0
        update()
        assert distance_m(obj, evaluated(obj), neutral) < TOLERANCE, obj.name + ': neutral not restored'
    groom_checks = []
    # Hidden collection objects are not evaluated after reopen: their cached
    # matrix_world can be identity. Evaluate the archive for frame comparison.
    archive = bpy.data.collections['SOURCE_AUTHORING - preserved, hidden']
    archive.hide_viewport = False
    update()
    for item in report['grooms']:
        obj = bpy.data.objects[item['baked']]
        original = bpy.data.objects[item['source']]
        assert len(obj.data.curves) == len(original.data.curves) == item['curves']
        a = np.empty(len(obj.data.points) * 3, dtype=np.float32)
        b = np.empty(len(original.data.points) * 3, dtype=np.float32)
        obj.data.attributes['position'].data.foreach_get('vector', a)
        original.data.attributes['position'].data.foreach_get('vector', b)
        assert np.array_equal(a, b), obj.name + ': groom control points changed'
        frame_error = float(np.max(np.abs(np.array(obj.matrix_world) - np.array(original.matrix_world))))
        assert frame_error < 1e-6, (obj.name, 'groom frame', frame_error)
        assert obj.data.surface == bpy.data.objects['BAKED_head_lod0_mesh']
        groom_checks.append({'name': obj.name, 'curves': item['curves'], 'control_points_and_world_frame_preserved': True})
    archive.hide_viewport = True
    result = {'passed': True, 'max_evaluated_error_m': max(errors.values()),
              'mesh_shape_checks': len(errors), 'neutral_weights_and_names_checked': True,
              'groom_checks': groom_checks,
              'prepared_sha256': digest(PREPARED), 'source_unchanged': digest(report['source']) == report['source_sha256'],
              'visual_review_required': True}
    assert result['source_unchanged']
    (OUT / 'reopen_verification.json').write_text(json.dumps(result, indent=2))
    print('REOPEN_VERIFIED ' + json.dumps(result), flush=True)


def main():
    assert Path(bpy.data.filepath).name == 'Jiwoo_Rigged.blend'
    assert not PREPARED.exists(), 'Refusing to overwrite existing review file'
    OUT.mkdir(parents=True, exist_ok=True)
    source = Path(bpy.data.filepath)
    source_hash = digest(source)
    names = json.loads(Path(__file__).with_name('jiwoo_shape_names.json').read_text())
    mapping = poses()
    assert len(names) == 73 and set(names) == set(mapping)
    rig = bpy.data.objects['rig']
    baseline_pose = {b.name: b.matrix_basis.copy() for b in rig.pose.bones}
    objects = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.name.startswith(PREFIX)]
    source_collection = bpy.data.collections.new('SOURCE_AUTHORING - preserved, hidden')
    bpy.context.scene.collection.children.link(source_collection)
    for obj in list(bpy.context.scene.objects):
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        source_collection.objects.link(obj)
    baked_collection = bpy.data.collections.new('JIWOO_BAKED - review shape keys here')
    bpy.context.scene.collection.children.link(baked_collection)
    armatures = {m.object for o in objects for m in o.modifiers if m.type == 'ARMATURE' and m.object}
    frozen = {}
    for original in armatures:
        clone = original.copy()
        clone.data = original.data.copy()
        clone.name = 'BAKED_' + original.name
        clone.animation_data_clear()
        clone.parent = None
        clone.matrix_world = original.matrix_world.copy()
        baked_collection.objects.link(clone)
        frozen[original] = clone
        ev = original.evaluated_get(bpy.context.evaluated_depsgraph_get())
        matrices = {b.name: b.matrix.copy() for b in ev.pose.bones}
        for b in clone.pose.bones:
            for c in list(b.constraints):
                b.constraints.remove(c)
        update()
        for b in clone.pose.bones:
            options = {}
            if b.parent:
                options = {'parent_matrix': matrices[b.parent.name],
                           'parent_matrix_local': b.parent.bone.matrix_local}
            b.matrix_basis = b.bone.convert_local_to_pose(
                matrices[b.name], b.bone.matrix_local, invert=True, **options)
        update()
        clone.hide_render = True
        clone.hide_set(True)
    clones = {}
    neutral = {}
    raw = {}
    inverse = {}
    mesh_report = []
    for obj in objects:
        clone = obj.copy()
        clone.data = obj.data.copy()
        clone.name = 'BAKED_' + obj.name.removeprefix(PREFIX)
        clone.animation_data_clear()
        clone.parent = frozen.get(obj.parent)
        baked_collection.objects.link(clone)
        clone.matrix_world = obj.matrix_world.copy()
        for m in clone.modifiers:
            if m.type == 'ARMATURE' and m.object:
                m.object = frozen[m.object]
        clone.hide_set(False)
        clones[obj] = clone
        raw[obj] = coords(obj.data.vertices)
        update()
        neutral[obj] = evaluated(obj)
        frozen_neutral = evaluated(clone)
        neutral_error = distance_m(obj, frozen_neutral, neutral[obj])
        assert neutral_error < .0001, (obj.name, 'frozen rig mismatch exceeds 0.1 mm', neutral_error)
        # Invert the *neutral* modifier stack, not the posed one. This avoids
        # double deformation while preserving all skin weights and body rigging.
        basis = clone.shape_key_add(name='Basis')
        basis.value = 0
        probe = clone.shape_key_add(name='_calibration')
        columns = []
        for axis in range(3):
            offset = np.zeros(3)
            offset[axis] = .01
            probe.data.foreach_set('co', (raw[obj] + offset).astype(np.float32).ravel())
            clone.data.update()
            probe.value = 1
            update()
            columns.append((evaluated(clone) - frozen_neutral) / .01)
        probe.value = 0
        clone.shape_key_remove(probe)
        inverse[obj] = np.linalg.inv(np.stack(columns, axis=2))
        # Freeze-rounding can be amplified by the outfit's 0.01 object scale.
        # Correct that neutral offset once, using the measured inverse stack.
        raw[obj] += np.einsum('nij,nj->ni', inverse[obj], neutral[obj] - frozen_neutral)
        basis.data.foreach_set('co', raw[obj].astype(np.float32).ravel())
        clone.data.update()
        update()
        corrected_error = distance_m(clone, evaluated(clone), neutral[obj])
        assert corrected_error < TOLERANCE, (obj.name, 'corrected neutral', corrected_error)
        mesh_report.append({'source': obj.name, 'baked': clone.name, 'vertices': len(obj.data.vertices),
                            'frozen_neutral_correction_m': neutral_error,
                            'weights_sha256': weights_signature(obj),
                            'materials': [m.name if m else None for m in obj.data.materials]})
    expected = {clone.name + '|Basis': neutral[obj].astype(np.float32) for obj, clone in clones.items()}
    shape_report = []
    for name in names:
        for b in rig.pose.bones:
            b.matrix_basis = baseline_pose[b.name]
        # Add operations in the neutral control frame (independent of order).
        translations = {}
        rotations = {}
        for op in mapping[name]:
            bone = op['bone']
            assert bone in rig.pose.bones, bone
            translations[bone] = translations.get(bone, Vector()) + Vector(op.get('translation_m', (0, 0, 0)))
            rotations[bone] = rotations.get(bone, Vector()) + Vector(op.get('rotation_xyz_rad', (0, 0, 0)))
        for bone, delta in translations.items():
            b = rig.pose.bones[bone]
            orientation = b.bone.matrix_local.to_3x3()
            b.location += orientation.inverted() @ delta
            angle = rotations[bone]
            if angle.length:
                q = Quaternion(angle.normalized(), angle.length)
                local_q = orientation.to_quaternion().inverted() @ q @ orientation.to_quaternion()
                b.rotation_mode = 'QUATERNION'
                b.rotation_quaternion = baseline_pose[bone].to_quaternion() @ local_q
        update()
        maxima = {}
        for obj, clone in clones.items():
            posed = evaluated(obj)
            delta = posed - neutral[obj]
            maximum = distance_m(obj, posed, neutral[obj])
            maxima[clone.name] = maximum
            key = clone.shape_key_add(name=name)
            key.value = 0
            local_delta = np.einsum('nij,nj->ni', inverse[obj], delta)
            target = raw[obj] + local_delta
            assert np.isfinite(target).all()
            key.data.foreach_set('co', target.astype(np.float32).ravel())
            clone.data.update()
            expected[clone.name + '|' + name] = posed.astype(np.float32)
        maximum = max(maxima.values())
        if name != 'viseme_sil' and maximum < 1e-6:
            # Do not deliver a zero-deformation placeholder under a requested name.
            for clone in clones.values():
                clone.shape_key_remove(clone.data.shape_keys.key_blocks[name])
            raise RuntimeError('No deformation for ' + name)
        shape_report.append({'name': name, 'status': 'intentional_neutral' if name == 'viseme_sil' else 'authored_approximation_needs_visual_review',
                             'max_displacement_m': maximum, 'per_mesh_displacement_m': maxima})
        print('BAKED ' + name + ' max=' + str(maximum), flush=True)
    for b in rig.pose.bones:
        b.matrix_basis = baseline_pose[b.name]
    update()
    for obj in objects:
        assert distance_m(obj, evaluated(obj), neutral[obj]) < TOLERANCE
    # Copy only the physically fitted groom set. Keep all originals in SOURCE.
    groom_report = []
    for name in ['Group_0_001', 'Group_0.001', 'main.001']:
        obj = bpy.data.objects[name]
        clone = obj.copy()
        clone.data = obj.data.copy()
        clone.name = {'Group_0_001': 'BAKED_ScalpGroom', 'Group_0.001': 'BAKED_EyebrowGroom', 'main.001': 'BAKED_EyelashGroom'}[name]
        baked_collection.objects.link(clone)
        clone.parent = clones[bpy.data.objects[PREFIX + 'head_lod0_mesh']]
        clone.matrix_world = obj.matrix_world.copy()
        if clone.data.surface in clones:
            clone.data.surface = clones[clone.data.surface]
        clone.hide_set(False)
        groom_report.append({'source': name, 'baked': clone.name, 'curves': len(obj.data.curves),
                             'facial_shape_following': 'Not baked for curves; requires review and later surface-deformation setup.'})
    source_collection.hide_render = True
    source_collection.hide_viewport = True
    # Disable external DNA driver expressions in this prepared copy. Their current
    # evaluated neutral transforms have already been frozen in the review rigs.
    for obj in source_collection.objects:
        if obj.animation_data:
            for driver in obj.animation_data.drivers:
                driver.mute = True
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    face = clones[bpy.data.objects[PREFIX + 'head_lod0_mesh']]
    face.select_set(True)
    bpy.context.view_layer.objects.active = face
    face.active_shape_key_index = 0
    report = {'source': str(source), 'source_sha256': source_hash, 'prepared': str(PREPARED),
              'blender': bpy.app.version_string, 'shape_names': names, 'meshes': mesh_report,
              'shapes': shape_report, 'grooms': groom_report, 'visual_review_required': True,
              'known_review_issues': {
                  'meshes_without_facial_control_deformation': [clone.name for clone in clones.values()
                      if all(s['per_mesh_displacement_m'][clone.name] < 1e-6 for s in shape_report)],
                  'outfit_facial_motion_max_m': max(s['per_mesh_displacement_m']['BAKED_Outfits'] for s in shape_report),
                  'note': 'Body remaining neutral is expected. Eye-edge/cartilage not following facial controls and outfit following facial controls need visual review; no corrective geometry has been invented.'},
              'method': 'New Rigify control poses, sampled on delivered mesh; inverse-neutral-stack baking into preserved skin weights. Frozen independent review armatures prevent control-pose double deformation.',
              'limitations': ['All 72 non-neutral poses are newly authored approximations, not recovered original facial animation.',
                             'Embedded rig_ui.py and external DNA solver were not executed. Neutral-state comparison is against the file as safely loaded.',
                             'Review lip seals, eyelid contact, teeth/tongue intersections, emotions, and eye gaze.',
                             'Facial strand deformation and final export skeleton consolidation are deferred until review.',
                             'Do not animate facial bones on top of their baked equivalents.']}
    (OUT / 'shape_pose_mapping.json').write_text(json.dumps(mapping, indent=2))
    np.savez_compressed(OUT / 'evaluated_baselines.npz', **expected)
    (OUT / 'bake_report.json').write_text(json.dumps(report, indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(PREPARED))
    assert digest(source) == source_hash
    print('BAKE_SAVED ' + str(PREPARED), flush=True)


if '--verify' in sys.argv:
    verify()
else:
    main()
