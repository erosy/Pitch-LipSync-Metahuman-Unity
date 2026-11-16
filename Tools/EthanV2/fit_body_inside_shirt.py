import json,numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
p=Path('Data/EthanV2/ClothingRepair');d=json.loads((p/'binding_input.json').read_text());samples=json.loads((p/'pose_samples.json').read_text());cw=json.loads((p/'transferred_weights.json').read_text())['weights'];b=np.array(d['bodyVertices']);c=np.array(d['clothVertices']);bt=np.array(d['bodyTriangles']).reshape(-1,3);ct=np.array(d['clothTriangles']).reshape(-1,3);cd=np.array(json.loads((p/'fit_offsets.json').read_text()));tree=BVHTree.FromPolygons(c.tolist(),ct.tolist(),all_triangles=True)
bw=[];at=0
for n in d['bodyCounts']:bw.append([(x['bone'],x['weight']) for x in d['bodyWeights'][at:at+n]]);at+=n
selected=np.array([i for i in np.flatnonzero((np.abs(b[:,0])>.08)&(b[:,1]>1.25)&(b[:,1]<1.59)) if tree.find_nearest(Vector(b[i]))[3]<.045]);centers=b[bt].mean(axis=1);faces=bt[(np.abs(centers[:,0])>.08)&(centers[:,1]>1.25)&(centers[:,1]<1.59)];faces=np.array([face for face in faces if tree.find_nearest(Vector(b[face].mean(0)))[3]<.045]);selected=np.unique(np.concatenate([selected,faces.flatten()]));slots={i:j for j,i in enumerate(selected)};face_slots=np.array([[slots[i] for i in face] for face in faces]);delta=np.zeros_like(b)
def normals(v):
 n=np.cross(v[bt[:,1]]-v[bt[:,0]],v[bt[:,2]]-v[bt[:,0]]);out=np.zeros_like(v)
 for j in range(3):np.add.at(out,bt[:,j],n)
 return out/np.maximum(np.linalg.norm(out,axis=1),1e-12)[:,None]
prepared=[]
for s in samples:
 mat=np.array(s['matrices']).reshape(-1,3,3);cl=np.array([sum(mat[i]*w for i,w in row) for row in cw]);cp=np.array(s['after'])+np.einsum('nij,nj->ni',cl,cd);bl=np.array([sum(mat[i]*w for i,w in bw[j]) for j in selected]);body=np.array(s['body']);prepared.append((body[selected],normals(body)[selected],BVHTree.FromPolygons(cp.tolist(),ct.tolist(),all_triangles=True),bl,np.linalg.inv(bl)))
edges=np.concatenate([bt[:,[0,1]],bt[:,[1,2]],bt[:,[2,0]]]);edges=np.concatenate([edges,edges[:,::-1]]);count=np.zeros(len(b));np.add.at(count,edges[:,0],1)
# Native head/body boundary positions must not change.
unique,counts=np.unique(np.sort(edges[:len(edges)//2],axis=1),axis=0,return_counts=True);boundary=np.unique(unique[counts==1]);groups={}
for i,v in enumerate(b):groups.setdefault(tuple(np.round(v,6)),[]).append(i)
for iteration in range(8):
 correction=np.zeros_like(b);worst=np.zeros(len(b))
 for body,norm,tree,linear,inverse in prepared:
  pose=body+np.einsum('nij,nj->ni',linear,delta[selected])
  for at,i in enumerate(selected):
   pos,normal,ti,dist=tree.find_nearest(Vector(pose[at]));normal=np.array(normal)
   if normal@norm[at]<0:normal=-normal
   violation=.004+np.dot(pose[at]-np.array(pos),normal)
   if violation>0 and dist<.035 and violation>worst[i]:worst[i]=violation;correction[i]=-inverse[at]@(normal*violation)
  for face,at in zip(faces,face_slots):
   v=pose[at].mean(axis=0);pos,normal,ti,dist=tree.find_nearest(Vector(v));normal=np.array(normal)
   if normal@norm[at].mean(axis=0)<0:normal=-normal
   violation=.006+np.dot(v-np.array(pos),normal)
   if violation>0 and dist<.035:
    for i,k in zip(face,at):
     if violation>worst[i]:worst[i]=violation;correction[i]=-inverse[k]@(normal*violation)
 delta+=correction*.8
 total=np.zeros_like(b);np.add.at(total,edges[:,0],delta[edges[:,1]]);delta=.7*delta+.3*total/np.maximum(count,1)[:,None];delta*=np.minimum(1,.02/np.maximum(np.linalg.norm(delta,axis=1),1e-12))[:,None]
 for g in groups.values():delta[g]=delta[g].mean(axis=0)
 delta[boundary]=0
 print(iteration,'selected',len(selected),'max',np.linalg.norm(delta,axis=1).max(),flush=True)
(p/'body_fit_offsets.json').write_text(json.dumps(delta.tolist()))
