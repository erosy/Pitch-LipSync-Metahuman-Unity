import bpy,numpy as np
for o in bpy.context.scene.objects:
 if o.type=='CURVES' and o.name.startswith('BAKED'):
  a=np.empty(len(o.data.points)*3,np.float32);o.data.attributes['position'].data.foreach_get('vector',a);a=a.reshape(-1,3);w=np.array(o.matrix_world);a=a@w[:3,:3].T+w[:3,3]
  print('GROOM',o.name,'strands',len(o.data.curves),'world_bounds',a.min(0).tolist(),a.max(0).tolist(),'surface',getattr(o.data.surface,'name',None))
