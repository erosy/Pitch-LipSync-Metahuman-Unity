import json,numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
p=Path('Data/EthanV2/ClothingRepair');d=json.loads((p/'binding_input.json').read_text());b=np.array(d['bodyVertices']);bt=np.array(d['bodyTriangles']).reshape(-1,3);c=np.array(d['clothVertices']);ct=np.array(d['clothTriangles']).reshape(-1,3);tree=BVHTree.FromPolygons(c.tolist(),ct.tolist(),all_triangles=True)
n=np.cross(b[bt[:,1]]-b[bt[:,0]],b[bt[:,2]]-b[bt[:,0]]);norm=np.zeros_like(b)
for k in range(3):np.add.at(norm,bt[:,k],n)
norm/=np.maximum(np.linalg.norm(norm,axis=1)[:,None],1e-12);amount=np.zeros(len(b))
for i in np.flatnonzero((np.abs(b[:,0])>.13)&(b[:,1]>1.32)):
 hit=tree.ray_cast(Vector(b[i]-norm[i]*.03),Vector(norm[i]),.11)
 if hit[0] is not None:amount[i]=.01
edges=np.concatenate([bt[:,[0,1]],bt[:,[1,2]],bt[:,[2,0]]]);edges=np.concatenate([edges,edges[:,::-1]]);count=np.zeros(len(b));np.add.at(count,edges[:,0],1)
for _ in range(8):
 total=np.zeros(len(b));np.add.at(total,edges[:,0],amount[edges[:,1]]);amount=.5*amount+.5*total/np.maximum(count,1)
groups={}
for i,v in enumerate(b):groups.setdefault(tuple(np.round(v,6)),[]).append(i)
for g in groups.values():amount[g]=amount[g].mean();norm[g]=norm[g].mean(axis=0)
(p/'body_fit_offsets.json').write_text(json.dumps((-norm*amount[:,None]).tolist()));print('max inset',amount.max(),'affected >1mm',sum(amount>.001))
