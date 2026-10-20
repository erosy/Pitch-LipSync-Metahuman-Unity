"""Read-only Blender source audit. Does not save or change the source blend."""
import bpy
import hashlib
import json
from pathlib import Path
from mathutils import Vector


OUTPUT = Path(__file__).resolve().parents[2] / 'Data' / 'JiwooReimport' / 'Batch01'


def vec(value):
    return [round(float(x), 8) for x in value]


def bounds(points):
    points = list(points)
    if not points:
        return None
    low = [min(p[i] for p in points) for i in range(3)]
    high = [max(p[i] for p in points) for i in range(3)]
    return {'min': vec(low), 'max': vec(high),
            'size': vec([high[i] - low[i] for i in range(3)])}


def main():
    scene = bpy.context.scene
    source = Path(bpy.data.filepath)
    report = {
        'source': str(source),
        'source_sha256': hashlib.file_digest(source.open('rb'), 'sha256').hexdigest(),
        'blender_version': bpy.app.version_string,
        'saved_version': list(bpy.data.version),
        'units': {'system': scene.unit_settings.system,
                  'scale_length': scene.unit_settings.scale_length,
                  'length_unit': scene.unit_settings.length_unit},
        'frame': scene.frame_current,
        'frame_range': [scene.frame_start, scene.frame_end],
        'objects': [],
        'embedded_text_names': [t.name for t in bpy.data.texts],
        'actions': [a.name for a in bpy.data.actions],
        'missing_external_images': [],
    }
    for image in bpy.data.images:
        if image.source == 'FILE' and not image.packed_file and image.filepath:
            path = bpy.path.abspath(image.filepath)
            if not Path(path).exists():
                report['missing_external_images'].append({'image': image.name, 'path': path})
    for obj in scene.objects:
        item = {
            'name': obj.name, 'type': obj.type,
            'parent': obj.parent.name if obj.parent else None,
            'parent_type': obj.parent_type, 'parent_bone': obj.parent_bone,
            'location': vec(obj.location), 'rotation_euler': vec(obj.rotation_euler),
            'scale': vec(obj.scale), 'world_scale': vec(obj.matrix_world.to_scale()),
            'world_matrix': [vec(row) for row in obj.matrix_world],
            'world_bounds': bounds(obj.matrix_world @ Vector(p) for p in obj.bound_box)
                if obj.type in {'MESH', 'CURVE', 'CURVES'} else None,
            'hide_viewport': obj.hide_viewport, 'hide_render': obj.hide_render,
            'hidden': obj.hide_get(),
            'modifiers': [{'name': m.name, 'type': m.type,
                           'viewport': m.show_viewport, 'render': m.show_render,
                           'target': getattr(getattr(m, 'object', None), 'name', None)}
                          for m in obj.modifiers],
            'drivers': len(obj.animation_data.drivers) if obj.animation_data else 0,
        }
        if obj.type == 'MESH':
            item['vertices'] = len(obj.data.vertices)
            item['shape_keys'] = [k.name for k in obj.data.shape_keys.key_blocks] if obj.data.shape_keys else []
            item['vertex_groups'] = len(obj.vertex_groups)
            item['materials'] = [m.name if m else None for m in obj.data.materials]
        elif obj.type == 'ARMATURE':
            item['bone_count'] = len(obj.data.bones)
            item['pose_position'] = obj.data.pose_position
            item['bones'] = [
                {'name': b.name, 'parent': b.parent.name if b.parent else None,
                 'head': vec(b.head_local), 'tail': vec(b.tail_local),
                 'pose_scale': vec(obj.pose.bones[b.name].scale),
                 'world_scale': vec((obj.matrix_world @ obj.pose.bones[b.name].matrix).to_scale())}
                for b in obj.data.bones if b.name.lower() in {'root', 'pelvis', 'head', 'neck_01', 'neck_02'}]
        elif obj.type == 'CURVE':
            item['spline_count'] = len(obj.data.splines)
            item['spline_types'] = sorted(set(s.type for s in obj.data.splines))
            item['bevel_depth'] = obj.data.bevel_depth
            item['resolution_u'] = obj.data.resolution_u
            item['samples'] = []
            step = max(1, len(obj.data.splines) // 50)
            for spline in list(obj.data.splines)[::step][:50]:
                points = spline.bezier_points if spline.type == 'BEZIER' else spline.points
                positions = [obj.matrix_world @ Vector(p.co[:3]) for p in points]
                item['samples'].append({
                    'type': spline.type, 'points': len(points), 'cyclic': spline.use_cyclic_u,
                    'world_root': vec(positions[0]) if positions else None,
                    'world_tip': vec(positions[-1]) if positions else None,
                    'control_polygon_length': sum((b-a).length for a,b in zip(positions, positions[1:])),
                    'radius_range': [min(p.radius for p in points), max(p.radius for p in points)] if points else None})
        elif obj.type == 'CURVES':
            item['curve_count'] = len(obj.data.curves)
            item['point_count'] = len(obj.data.points)
            item['surface'] = getattr(obj.data.surface, 'name', None)
            item['attributes'] = [{'name': a.name, 'type': a.data_type, 'domain': a.domain} for a in obj.data.attributes]
        report['objects'].append(item)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    (OUTPUT / 'source_audit.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('JIWOO_AUDIT_COMPLETE ' + str(OUTPUT / 'source_audit.json'), flush=True)
    print('SOURCE_SAVED_VERSION ' + str(report['saved_version']), flush=True)
    for item in report['objects']:
        print('OBJECT ' + json.dumps({k: item[k] for k in ['name', 'type', 'scale', 'world_scale', 'world_bounds']}), flush=True)


main()
