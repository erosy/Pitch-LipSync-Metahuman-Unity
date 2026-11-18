import bpy,json,numpy as np,pathlib
report=[]
for o in bpy.data.objects:
 if o.type=='CURVES' and o.name.startswith('BAKED'):
  d=o.evaluated_get(bpy.context.evaluated_depsgraph_get()).data;p=np.empty(len(d.points)*3,np.float32);d.attributes['position'].data.foreach_get('vector',p);m=np.array(o.matrix_world);p=p.reshape(-1,3)@m[:3,:3].T+m[:3,3]
  r=d.attributes.get('radius');v=np.empty(len(r.data),np.float32) if r else None
  if r:r.data.foreach_get('value',v)
  report.append({'name':o.name,'curves':len(d.curves),'points':len(d.points),'min':p.min(0).tolist(),'max':p.max(0).tolist(),'radius':([float(v.min()),float(np.median(v)),float(v.max())] if r else None),'matrix':m.tolist(),'surface':getattr(d.surface,'name',None),'materials':[m.name for m in d.materials]})
pathlib.Path('D:/UnityProject/MetahumanTemplate/Data/Liora/Integration/blender_grooms.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
