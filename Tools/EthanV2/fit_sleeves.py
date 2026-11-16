import json,numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
p=Path('Data/EthanV2/ClothingRepair');d=json.loads((p/'binding_input.json').read_text());samples=json.loads((p/'pose_samples.json').read_text());weights=json.loads((p/'transferred_weights.json').read_text())['weights'];b=np.array(d['bodyVertices']);c=np.array(d['clothVertices']);bt=np.array(d['bodyTriangles']).reshape(-1,3);ct=np.array(d['clothTriangles']).reshape(-1,3)
selected=np.flatnonzero((np.abs(c[:,0])>.08)&(c[:,1]>1.25));delta=np.zeros_like(c);prepared=[]
cloth_tree=BVHTree.FromPolygons(c.tolist(),ct.tolist(),all_triangles=True);body_links=[]
for j in np.flatnonzero((np.abs(b[:,0])>.08)&(b[:,1]>1.25)):
 pos,normal,ti,dist=cloth_tree.find_nearest(Vector(b[j]));normal=np.array(normal)
 if dist<.035 and np.dot(b[j]-np.array(pos),normal)<.003:
  face=ct[ti];a,x,y=c[face];uv=np.linalg.lstsq(np.column_stack((x-a,y-a)),np.array(pos)-a,rcond=None)[0];bary=np.maximum(0,[1-uv.sum(),*uv]);bary/=bary.sum();body_links.append((j,face,bary))
def body_normals(vertices):
 n=np.cross(vertices[bt[:,1]]-vertices[bt[:,0]],vertices[bt[:,2]]-vertices[bt[:,0]]);out=np.zeros_like(vertices)
 for j in range(3):np.add.at(out,bt[:,j],n)
 return out/np.maximum(np.linalg.norm(out,axis=1),1e-12)[:,None]
for s in samples:
 body=np.array(s['body']);mat=np.array(s['matrices']).reshape(-1,3,3);linear=np.array([sum(mat[i]*w for i,w in row) for row in weights]);prepared.append((np.array(s['after']),BVHTree.FromPolygons(body.tolist(),bt.tolist(),all_triangles=True),linear,np.linalg.inv(linear),body,body_normals(body)))
edges=np.concatenate([ct[:,[0,1]],ct[:,[1,2]],ct[:,[2,0]]]);edges=np.concatenate([edges,edges[:,::-1]]);count=np.zeros(len(c));np.add.at(count,edges[:,0],1)
groups={}
for i,v in enumerate(c):groups.setdefault(tuple(np.round(v,5)),[]).append(i)
centers=c[ct].mean(axis=1);selected_faces=ct[(np.abs(centers[:,0])>.08)&(centers[:,1]>1.25)]
for iteration in range(12):
 worst=np.zeros(len(c));correction=np.zeros_like(c)
 for verts,tree,linear,inverse,body,body_normal in prepared:
  pose=verts+np.einsum('nij,nj->ni',linear,delta)
  for i in selected:
   pos,normal,tri,dist=tree.find_nearest(Vector(pose[i]));normal=np.array(normal);signed=np.dot(pose[i]-np.array(pos),normal)
   if signed<.006 and dist<.045 and .006-signed>worst[i]:worst[i]=.006-signed;correction[i]=inverse[i]@(normal*(.006-signed))
  for face in selected_faces:
   v=pose[face].mean(axis=0);pos,normal,tri,dist=tree.find_nearest(Vector(v));normal=np.array(normal);signed=np.dot(v-np.array(pos),normal)
   if signed<.006 and dist<.045:
    for i in face:
     if .006-signed>worst[i]:worst[i]=.006-signed;correction[i]=inverse[i]@(normal*(.006-signed))
  for j,face,bary in body_links:
   surface=(pose[face]*bary[:,None]).sum(axis=0);normal=body_normal[j];violation=.006+np.dot(body[j]-surface,normal)
   if violation>0:
    for i,weight in zip(face,bary):
     need=violation*weight/max(np.dot(bary,bary),.33)
     if need>worst[i]:worst[i]=need;correction[i]=inverse[i]@(normal*need)
 delta+=correction*.8
 for _ in range(3):
  summed=np.zeros_like(delta);np.add.at(summed,edges[:,0],delta[edges[:,1]]);delta=delta*.5+summed/np.maximum(count,1)[:,None]*.5
 delta*=np.minimum(1,.08/np.maximum(np.linalg.norm(delta,axis=1),1e-12))[:,None]
 for g in groups.values():delta[g]=delta[g].mean(0)
 print(iteration,'max offset',np.linalg.norm(delta,axis=1).max(),flush=True)
(p/'fit_offsets.json').write_text(json.dumps(delta.tolist()))
