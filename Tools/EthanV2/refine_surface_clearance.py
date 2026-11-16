import json,numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
p=Path('Data/EthanV2/ClothingRepair');d=json.loads((p/'binding_input.json').read_text());s=json.loads((p/'pose_samples.json').read_text());j=json.loads((p/'surface_bindings.json').read_text());b=np.array(d['bodyVertices']);c=np.array(d['clothVertices']);ct=np.array(d['clothTriangles']).reshape(-1,3);bt=np.array(d['bodyTriangles']).reshape(-1,3);tri=np.array(j['triangles']);bar=np.array(j['bary']);offset=np.array(j['offsets']);extra=np.zeros(len(c))
def smooth(x):return np.clip(x,0,1)**2*(3-2*np.clip(x,0,1))
influence=smooth((np.abs(c[:,0])-.06)/.05)*smooth((c[:,1]-1.18)/.1)
tree=BVHTree.FromPolygons(c.tolist(),ct.tolist(),all_triangles=True);normal=np.zeros_like(b);faces=np.cross(b[bt[:,1]]-b[bt[:,0]],b[bt[:,2]]-b[bt[:,0]])
for k in range(3):np.add.at(normal,bt[:,k],faces)
normal/=np.maximum(np.linalg.norm(normal,axis=1),1e-12)[:,None]
selected=[i for i in np.flatnonzero((np.abs(b[:,0])>.08)&(b[:,1]>1.34)&(b[:,1]<1.59)) if tree.ray_cast(Vector(b[i]-normal[i]*.02),Vector(normal[i]),.1)[0] is not None]
prepared=[]
for sample in s:
 body=np.array(sample['body']);a=body[tri];t=a[:,1]-a[:,0];t/=np.maximum(np.linalg.norm(t,axis=1),1e-12)[:,None];n=np.cross(t,a[:,2]-a[:,0]);n/=np.maximum(np.linalg.norm(n,axis=1),1e-12)[:,None];y=np.cross(n,t);surface=(a*bar[:,:,None]).sum(axis=1)+t*offset[:,0,None]+y*offset[:,1,None]+n*offset[:,2,None];prepared.append((body,np.array(sample['after']),surface,n))
edges=np.concatenate([ct[:,[0,1]],ct[:,[1,2]],ct[:,[2,0]]]);edges=np.concatenate([edges,edges[:,::-1]]);count=np.zeros(len(c));np.add.at(count,edges[:,0],1);groups={}
for i,v in enumerate(c):groups.setdefault(tuple(np.round(v,5)),[]).append(i)
for iteration in range(5):
 increment=np.zeros(len(c))
 for body,base,surface,n in prepared:
  posed=base*(1-influence[:,None])+(surface+n*extra[:,None])*influence[:,None];tree=BVHTree.FromPolygons(posed.tolist(),ct.tolist(),all_triangles=True)
  for point in body[selected]:
   pos,normal,ti,dist=tree.find_nearest(Vector(point));normal=np.array(normal);signed=(point-np.array(pos))@normal
   if signed>-.005 and dist<.035:
    face=ct[ti]
    for i in face:
     alignment=n[i]@normal
     if influence[i]<.25 and c[i,1]>1.25:influence[i]=1
     if alignment>.2:increment[i]=max(increment[i],(.005+signed)/(alignment*max(influence[i],.25)))
 extra=np.minimum(.02,extra+increment*.8)
 # Feather without lowering the required correction at the collision points.
 for _ in range(3):
  total=np.zeros(len(c));np.add.at(total,edges[:,0],extra[edges[:,1]]);extra=np.maximum(extra,total/np.maximum(count,1)*.8)
 for g in groups.values():extra[g]=max(extra[g]);influence[g]=max(influence[g])
 print(iteration,'max extra',extra.max(),flush=True)
offset[:,2]+=extra;j['offsets']=offset.tolist();j['influences']=influence.tolist();(p/'surface_bindings.json').write_text(json.dumps(j))
