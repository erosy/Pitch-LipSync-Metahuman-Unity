"""Export only the validated bake; preserve all original deliverables."""
import bpy, pathlib, json, numpy as np
OUT=pathlib.Path('D:/UnityProject/MetahumanTemplate/Data/Liora/Native')
target=OUT/'Liora_73_Clean.fbx'
assert not target.exists(), 'Refuse overwrite'
meshes=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('BAKED_')]
rigs={m.object for o in meshes for m in o.modifiers if m.type=='ARMATURE' and m.object}
body=next(o for o in rigs if o.name.endswith('_body_rig'))
def evaluated(o):
    e=o.evaluated_get(bpy.context.evaluated_depsgraph_get());m=e.to_mesh()
    p=np.empty(len(m.vertices)*3,np.float32);m.vertices.foreach_get('co',p);e.to_mesh_clear()
    w=np.array(o.matrix_world);return p.reshape(-1,3)@w[:3,:3].T+w[:3,3]
before={o: evaluated(o) for o in meshes}
renames=[]
for rig in sorted(rigs,key=lambda o:o.name):
    prefix='' if rig==body else 'Face_' if rig.name.endswith('_head_rig') else 'Rigify_'
    if not prefix:continue
    users=[o for o in meshes if any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers)]
    for b in list(rig.data.bones):
        old=b.name;new=prefix+old;b.name=new
        for o in users:
            if old in o.vertex_groups:o.vertex_groups[old].name=new
        renames.append({'rig':rig.name,'old':old,'new':new})
bpy.context.view_layer.update()
errors={o.name:float(np.linalg.norm(evaluated(o)-before[o],axis=1).max()) for o in meshes}
assert max(errors.values())<2e-6,errors
# Remove authoring archive from this disposable process, never the source blend.
keep=set(meshes)|rigs
for o in list(bpy.data.objects):
    if o not in keep:bpy.data.objects.remove(o,do_unlink=True)
for collection in bpy.data.collections:collection.hide_viewport=False;collection.hide_render=False
for o in keep:
    o.hide_set(False);o.hide_viewport=False;o.select_set(True)
bpy.context.view_layer.objects.active=body
bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'MESH','ARMATURE'},use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',path_mode='AUTO')
(OUT/'clean_export_report.json').write_text(json.dumps({'fbx':str(target),'meshes':len(meshes),'rigs':[o.name for o in rigs],'excluded':'All source archive, control widgets, curves and animation clips','bone_renames':renames,'neutral_world_errors_m':errors,'scope':'Clean facial-bake export with explicit native-body Humanoid mapping; separate frozen rigs retained, skeleton consolidation is a later integration stage.'},indent=2))
print('CLEAN_EXPORT_SAVED',target,flush=True)
