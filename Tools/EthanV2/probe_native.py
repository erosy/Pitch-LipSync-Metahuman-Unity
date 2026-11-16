import bpy,importlib,pathlib,json,numpy as np
from mathutils.kdtree import KDTree
ROOT=pathlib.Path('D:/UnityProject/MetahumanTemplate');OUT=ROOT/'Data/EthanV2/Native'
A='bl_ext.api_portal_polyhammer_com_001.character_dna';u=importlib.import_module(A+'.utilities');engine=importlib.import_module(A+'.runtime.engine')
inst=u.get_addon_scene_properties().rig_instance_list[0]
inst.initialize()
importlib.import_module(A+'.runtime.controller').rebuild(inst)
r=inst.head_dna_reader
report={'bindings':engine.binding_issues(inst),'meshes':[],'test':[]}
def coords(o,ev=False):
    obj=o.evaluated_get(bpy.context.evaluated_depsgraph_get()) if ev else o
    m=obj.to_mesh() if ev else obj.data
    a=np.empty(len(m.vertices)*3,dtype=np.float32);m.vertices.foreach_get('co',a)
    if ev:obj.to_mesh_clear()
    return a.reshape(-1,3).astype(np.float64)
for idx in r.getMeshIndicesForLOD(0):
    meshname=r.getMeshName(idx)
    points=np.array([r.getVertexPositionXs(idx),r.getVertexPositionYs(idx),r.getVertexPositionZs(idx)]).T*.01
    kd=KDTree(len(points))
    for j,v in enumerate(points):kd.insert(v,j)
    kd.balance()
    matches=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Ethan_NewMetaHumanCharacter_'+meshname)]
    for o in matches:
        a=coords(o);nearest=[kd.find(v) for v in a];dist=np.array([v[2] for v in nearest])
        report['meshes'].append({'name':o.name,'dna_mesh':meshname,'vertices':len(a),'dna_vertices':len(points),'distance_max_m':float(dist.max()),'distance_p99_m':float(np.quantile(dist,.99)),'unmatched_0_1mm':int((dist>.0001).sum()),'raw_bounds':[a.min(axis=0).tolist(),a.max(axis=0).tolist()],'dna_bounds':[points.min(axis=0).tolist(),points.max(axis=0).tolist()]})
objs=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Ethan_')]
neutral={o.name:coords(o,True) for o in objs};board=inst.face_board
for name in ['CTRL_L_eye_blink','CTRL_C_jaw','CTRL_L_mouth_cornerPull']:
    b=board.pose.bones[name];base=b.matrix_basis.copy()
    for value in [-1,1]:
        b.matrix_basis=base;b.location.y+=value;board.update_tag();bpy.context.view_layer.update();bpy.context.scene.frame_set(bpy.context.scene.frame_current+1);bpy.context.view_layer.update()
        maxima={o.name:float(np.linalg.norm((coords(o,True)-neutral[o.name])@np.array(o.matrix_world.to_3x3()).T,axis=1).max()) for o in objs}
        report['test'].append({'bone':name,'y':value,'maxima_m':maxima})
    b.matrix_basis=base;board.update_tag();bpy.context.view_layer.update()
(OUT/'native_probe.json').write_text(json.dumps(report,indent=2))
print('NATIVE_PROBE',json.dumps(report),flush=True)
