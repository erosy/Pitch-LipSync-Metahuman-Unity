import bpy, pathlib, importlib, sys, json, numpy as np
sys.path.insert(0,str(pathlib.Path(__file__).parent))
from native_pose_mapping import make_mapping
OUT=pathlib.Path('D:/UnityProject/MetahumanTemplate/Data/Liora/Native')
A='bl_ext.api_portal_polyhammer_com_001.character_dna'
u=importlib.import_module(A+'.utilities');c=importlib.import_module(A+'.runtime.controller');a=importlib.import_module(A+'.runtime.authoring')
i=u.get_addon_scene_properties().rig_instance_list[0];i.initialize();c.rebuild(i);c.suspend(i)
r=i.head_dna_reader;mapping,_=make_mapping([r.getGUIControlName(j) for j in range(r.getGUIControlCount())])
gui=[r.getGUIControlName(j).rsplit('.',1) for j in range(r.getGUIControlCount())]
board=i.face_board;base={b.name:b.matrix_basis.copy() for b in board.pose.bones}
def pose(values):
    for b in board.pose.bones:b.matrix_basis=base[b.name]
    for bone,axis in gui:board.pose.bones[bone].location['xyz'.index(axis[-1])]=0
    board.pose.bones['CTRL_lookAtSwitch'].location.y=0
    for bone,axes in values.items():
        for axis,v in axes.items():board.pose.bones[bone].location['xyz'.index(axis)]=v
    board.update_tag();bpy.context.view_layer.update();a.sample(i,'head');a.bone_transforms(i,'head');i.update_head_shape_keys();i.head_rig.update_tag();bpy.context.view_layer.update()
def points(o):
    ev=o.evaluated_get(bpy.context.evaluated_depsgraph_get());d=ev.data
    p=np.empty(len(d.points)*3,np.float32);d.attributes['position'].data.foreach_get('vector',p)
    m=np.array(ev.matrix_world);return p.reshape(-1,3)@m[:3,:3].T+m[:3,3]
grooms=[o for o in bpy.context.scene.objects if o.type=='CURVES']
samples={};names=['Basis','eyeBlinkLeft','eyeBlinkRight','eyeSquintLeft','browInnerUp','browOuterUpLeft','emo_joy']
for name in names:
    pose(mapping.get(name,{}))
    for o in grooms:samples[o.name+'|'+name]=points(o)
pose({});np.savez_compressed(OUT/'groom_evaluated_baselines.npz',**samples)
originals=[o.name for o in grooms]
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Liora_73_Baked.blend'),load_ui=False,use_scripts=False)
face=bpy.data.objects['BAKED_head_lod0_mesh'];checks=[]
for name in names:
    for k in face.data.shape_keys.key_blocks:k.value=float(k.name==name and name!='Basis')
    bpy.context.view_layer.update()
    for original in originals:
        o=bpy.data.objects['BAKED_Groom_'+original];actual=points(o);expected=samples[original+'|'+name]
        error=float(np.linalg.norm(actual-expected,axis=1).max())
        checks.append({'groom':original,'pose':name,'max_world_error_m':error})
for k in face.data.shape_keys.key_blocks:k.value=0
stats=[]
for o in bpy.data.objects:
    if o.type=='MESH' and o.name.startswith('BAKED_'):
        o.data.calc_loop_triangles();n=len(o.data.vertices)
        stats.append({'object':o.name,'vertices':n,'triangles':len(o.data.loop_triangles),'dense_shape_position_bytes':n*3*4*73})
report={'passed':all(x['max_world_error_m']<2e-6 for x in checks),'checks':checks,'mesh_statistics':stats,'note':'Curve grooms retain surface deformation; no tubular mesh conversion or Unity integration performed.'}
(OUT/'groom_motion_verification.json').write_text(json.dumps(report,indent=2));print('GROOM_VERIFY',report['passed'],flush=True)
