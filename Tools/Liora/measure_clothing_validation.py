import pathlib,struct,json,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
R=pathlib.Path('D:/UnityProject/MetahumanTemplate/Data/Liora/ClothingFit')
report=json.loads((R/'activity_validation.json').read_text());d=json.loads((R/'binding_input.json').read_text());entries=report['entries']
with (R/'activity_validation.bin').open('rb') as f:nb,nc=struct.unpack('<ii',f.read(8))
samples=np.memmap(R/'activity_validation.bin',dtype='<f4',offset=8,shape=(len(entries),nb*3+nc*6));body=samples[:,:nb*3].reshape(-1,nb,3);before=samples[:,nb*3:nb*3+nc*3].reshape(-1,nc,3);after=samples[:,nb*3+nc*3:].reshape(-1,nc,3)
bt=np.fromfile(R/'surface_triangles.bin',dtype='<i4').reshape(-1,3);ct=np.array(d['clothTriangles']).reshape(-1,3)
pi=np.concatenate([np.repeat(np.arange(nc)[:,None],3,axis=1),ct,ct,ct,ct]);pb=np.concatenate([np.tile([1.,0,0],(nc,1)),np.tile([1/3]*3,(len(ct),1)),np.tile([.5,.5,0],(len(ct),1)),np.tile([0,.5,.5],(len(ct),1)),np.tile([.5,0,.5],(len(ct),1))])
results=[]
for j,entry in enumerate(entries):
 bvh=BVHTree.FromPolygons(body[j].tolist(),bt.tolist(),all_triangles=True);counts=[];minimum=[]
 for p in [before[j],after[j]]:
  probes=(p[pi]*pb[:,:,None]).sum(1);n=0;worst=0
  for point in probes:
   hit,normal,idx,dist=bvh.find_nearest(Vector(point))
   if dist>.035:continue
   signed=float(np.dot(point-np.array(hit),np.array(normal)))
   if signed<-.0005:n+=1;worst=min(worst,signed)
  counts.append(n);minimum.append(worst)
 results.append({**entry,'before_contacts':counts[0],'after_contacts':counts[1],'before_min_normal_distance_m':minimum[0],'after_min_normal_distance_m':minimum[1]})
 if j%12==0:print('ACTIVITY_PROBES',j,flush=True)
output={'poses':len(entries),'probes_per_pose':len(pi),'before_contacts':sum(x['before_contacts'] for x in results),'after_contacts':sum(x['after_contacts'] for x in results),'results':results,'metric_limit':'Nearest-skin contact proxy on garment vertices, centers and midpoints; open neck/armholes and inner garment folds do not define a closed penetration volume. These counts are not a zero-breakthrough certification.'}
(R/'activity_contacts.json').write_text(json.dumps(output,indent=2));print('ACTIVITY_CONTACTS',output['before_contacts'],output['after_contacts'],flush=True)
