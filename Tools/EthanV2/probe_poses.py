import bpy, json, pathlib, ast, numpy as np
from mathutils import Vector, Quaternion
root=pathlib.Path('D:/UnityProject/MetahumanTemplate')
tree=ast.parse((root/'Data/EthanV2/previous_baker.py').read_text())
func=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='poses')
ns={};exec(compile(ast.Module(body=[func],type_ignores=[]),'poses','exec'),ns)
mapping=ns['poses']();rig=bpy.data.objects['rig']
base={b.name:b.matrix_basis.copy() for b in rig.pose.bones}
objs=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Ethan_')]
def coords(o):
 e=o.evaluated_get(bpy.context.evaluated_depsgraph_get());m=e.to_mesh();a=np.empty(len(m.vertices)*3,dtype=np.float32);m.vertices.foreach_get('co',a);e.to_mesh_clear();return a.reshape(-1,3)
neutral={o.name:coords(o) for o in objs}
result={ 'bounds':{o.name:{'min':neutral[o.name].min(axis=0).tolist(),'max':neutral[o.name].max(axis=0).tolist()} for o in objs},'motion':{},'curves':[]}
for name in ['eyeBlinkLeft','jawOpen','tongueOut','mouthSmileLeft','eyeLookUpLeft']:
 for b in rig.pose.bones:b.matrix_basis=base[b.name]
 for op in mapping[name]:
  b=rig.pose.bones[op['bone']];ori=b.bone.matrix_local.to_3x3();b.location+=ori.inverted()@Vector(op.get('translation_m',(0,0,0)))
  a=Vector(op.get('rotation_xyz_rad',(0,0,0)))
  if a.length:
   q=ori.to_quaternion();b.rotation_mode='QUATERNION';b.rotation_quaternion=base[b.name].to_quaternion()@q.inverted()@Quaternion(a.normalized(),a.length)@q
 bpy.context.view_layer.update()
 result['motion'][name]={o.name:float(np.linalg.norm((coords(o)-neutral[o.name])@np.array(o.matrix_world.to_3x3()).T,axis=1).max()) for o in objs}
for o in bpy.data.objects:
 if o.type=='CURVES':result['curves'].append({'name':o.name,'curves':len(o.data.curves),'points':len(o.data.points),'surface':getattr(o.data.surface,'name',None),'parent_type':o.parent_type,'parent_bone':o.parent_bone})
(root/'Data/EthanV2/pose_probe.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
