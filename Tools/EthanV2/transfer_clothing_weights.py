import json, numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from pathlib import Path
p=Path('Data/EthanV2/ClothingRepair')
d=json.loads((p/'binding_input.json').read_text())
b=np.array(d['bodyVertices']); c=np.array(d['clothVertices']); t=np.array(d['bodyTriangles']).reshape(-1,3)
bvh=BVHTree.FromPolygons(b.tolist(),t.tolist(),all_triangles=True)
bw=[]; at=0
for n in d['bodyCounts']:
 bw.append({x['bone']:x['weight'] for x in d['bodyWeights'][at:at+n]});at+=n
result=[]; distances=[]; anchors=[]; cache={}
for v in c:
 key=tuple(np.round(v,5))
 if key in cache:
  w,anchor,dist=cache[key]
 else:
  pos,normal,idx,dist=bvh.find_nearest(Vector(v))
  tri=t[idx]; a,b1,c1=b[tri]; uv=np.linalg.lstsq(np.column_stack((b1-a,c1-a)),np.array(pos)-a,rcond=None)[0]
  bary=np.maximum(0,[1-uv.sum(),*uv]);bary/=sum(bary)
  weights={}
  for vertex,f in zip(tri,bary):
   for bone,weight in bw[vertex].items():weights[bone]=weights.get(bone,0)+weight*f
  w=sorted(weights.items(),key=lambda x:-x[1])[:4];s=sum(x[1] for x in w);w=[[int(i),float(x/s)] for i,x in w]
  anchor={'triangle':tri.tolist(),'bary':bary.tolist()}
  cache[key]=(w,anchor,dist)
 result.append(w);anchors.append(anchor);distances.append(dist)
(p/'transferred_weights.json').write_text(json.dumps({'weights':result,'anchors':anchors}))
print(json.dumps({'vertices':len(c),'distanceMedian':float(np.median(distances)),'distance95':float(np.percentile(distances,95)),'distanceMax':max(distances)}))
