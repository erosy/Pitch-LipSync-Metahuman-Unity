import json,numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
p=Path('Data/EthanV2/ClothingRepair');d=json.loads((p/'binding_input.json').read_text());b=np.array(d['bodyVertices']);bt=np.array(d['bodyTriangles']).reshape(-1,3);c=np.array(d['clothVertices']);ct=np.array(d['clothTriangles']).reshape(-1,3);bvh=BVHTree.FromPolygons(c.tolist(),ct.tolist(),all_triangles=True)
n=np.cross(b[bt[:,1]]-b[bt[:,0]],b[bt[:,2]]-b[bt[:,0]]);norm=np.zeros_like(b)
for k in range(3):np.add.at(norm,bt[:,k],n)
norm/=np.maximum(np.linalg.norm(norm,axis=1)[:,None],1e-12)
covered=[]
for v,direction in zip(b,norm):
 hit=bvh.ray_cast(Vector(v-direction*.002),Vector(direction),.16)
 covered.append(hit[0] is not None)
covered=np.array(covered);remove=covered[bt].all(axis=1)
# Keep one ring of body faces at garment openings as a conservative edge margin.
boundary=set(bt[~remove].flatten());remove &= np.array([not any(int(v) in boundary for v in row) for row in bt])
(p/'body_mask.json').write_text(json.dumps({'removeTriangles':np.flatnonzero(remove).tolist()}));print('masked',sum(remove),'of',len(bt))
