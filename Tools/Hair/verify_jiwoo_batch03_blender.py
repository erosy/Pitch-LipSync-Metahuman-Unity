"""Read back exported FBX and ABC in Blender; never save a blend file."""
import json
from pathlib import Path
import bpy
import numpy as np
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Data/JiwooReimport/Batch03'


def array(data, prop='co', n=3):
    a = np.empty(len(data)*n, dtype=np.float32)
    data.foreach_get(prop, a)
    return a.reshape(-1,n) if n>1 else a


def transformed(points, matrix):
    m=np.array(matrix)
    return points @ m[:3,:3].T + m[:3,3]


def main():
    source_mesh=bpy.data.objects['Face']
    source_weights=[{source_mesh.vertex_groups[g.group].name: g.weight for g in v.groups}
                    for v in source_mesh.data.vertices]
    source_bones={b.name: b.parent.name if b.parent else None
                  for b in bpy.data.objects['Jiwoo_Armature'].data.bones}
    source_materials=[m.name for m in source_mesh.data.materials]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(OUT/'Jiwoo_Normalized.fbx'),
                             use_anim=False, automatic_bone_orientation=False,
                             ignore_leaf_bones=False)
    mesh_obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    baseline=np.load(OUT/'character_baseline.npz')
    positions=transformed(array(mesh_obj.data.vertices), mesh_obj.matrix_world)
    # FBX may reorder vertices by material. Use geometry to find source indices.
    tree=KDTree(len(baseline['mesh']))
    for i,p in enumerate(baseline['mesh']): tree.insert(p,i)
    tree.balance()
    mapping=np.array([tree.find(p)[1] for p in positions])
    mesh_error=float(np.max(np.linalg.norm(positions-baseline['mesh'][mapping],axis=1)))
    same_order=positions.shape==baseline['mesh'].shape and np.max(np.abs(positions-baseline['mesh']))<1e-5
    if same_order:
        mapping=np.arange(len(positions))
    shapes={k.name: k for k in mesh_obj.data.shape_keys.key_blocks}
    shape_errors={}
    for name in baseline.files:
        if name in {'mesh','Basis'}: continue
        assert name in shapes, 'Missing shape '+name
        shape_pos=transformed(array(shapes[name].data),mesh_obj.matrix_world)
        shape_errors[name]=float(np.max(np.linalg.norm(shape_pos-baseline[name][mapping],axis=1)))
    # Coincident source vertices can have different weights. First test index
    # stability; otherwise compare candidate positions and preserve error reporting.
    weights_error=0.0
    if same_order:
        for i,v in enumerate(mesh_obj.data.vertices):
            weights={mesh_obj.vertex_groups[g.group].name:g.weight for g in v.groups}
            keys=weights.keys()|source_weights[i].keys()
            weights_error=max(weights_error,max((abs(weights.get(k,0)-source_weights[i].get(k,0)) for k in keys),default=0))
    bones={b.name: b.parent.name if b.parent else None for b in rig.data.bones}
    result={
        'fbx_vertex_count':len(positions),'fbx_bone_count':len(bones),
        'fbx_max_neutral_error_m':mesh_error,'fbx_shape_count':len(shapes)-1,
        'fbx_shape_max_errors_m':shape_errors,
        'fbx_vertex_order_preserved':bool(same_order),
        'fbx_weights_max_error':weights_error if same_order else None,
        'fbx_hierarchy_preserved':bones==source_bones,
        'fbx_material_slots_preserved':[m.name for m in mesh_obj.data.materials]==source_materials,
    }
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.alembic_import(filepath=str(OUT/'Jiwoo_Bob_HeadLocal.abc'),
                              scale=1,set_frame_range=False,as_background_job=False)
    bpy.context.view_layer.update()
    hair=next(o for o in bpy.context.scene.objects if o.type in {'CURVES','CURVE'})
    data=hair.evaluated_get(bpy.context.evaluated_depsgraph_get()).data
    result['abc_object_type']=hair.type
    if hair.type=='CURVES':
        pos=array(data.attributes['position'].data,'vector')
        result['abc_curve_count']=len(data.curves)
        if data.attributes.get('radius'):
            widths=array(data.attributes['radius'].data,'value',1)*2
            result['abc_diameter_min_m']=float(widths.min())
            result['abc_diameter_max_m']=float(widths.max())
    else:
        result['abc_curve_count']=len(data.splines)
        pos=np.concatenate([np.array([p.co[:3] for p in s.points]) for s in data.splines])
        widths=np.concatenate([np.array([p.radius for p in s.points]) for s in data.splines])*2
        result['abc_diameter_min_m']=float(widths.min())
        result['abc_diameter_max_m']=float(widths.max())
    expected=np.load(OUT/'hair_head_local_baseline.npy').reshape(-1,3)[:,[0,2,1]].copy()
    expected[:,0]*=-1
    result['abc_point_count']=len(pos)
    result['abc_max_readback_error_m']=float(np.max(np.linalg.norm(pos-expected,axis=1)))
    passed=(mesh_error<2e-5 and max(shape_errors.values())<2e-5 and
            result['fbx_hierarchy_preserved'] and result['fbx_material_slots_preserved'] and
            same_order and weights_error<1e-5 and result['abc_curve_count']==100493 and
            result['abc_max_readback_error_m']<2e-6)
    result['status']='PASS' if passed else 'FAIL'
    (OUT/'blender_readback_report.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print('BLENDER_READBACK '+json.dumps(result),flush=True)
    assert passed, 'Readback failed; inspect report.'


main()
