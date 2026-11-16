import json,numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
p=Path('Data/EthanV2/ClothingRepair');d=json.loads((p/'binding_input.json').read_text());samples=json.loads((p/'pose_samples.json').read_text());weights=json.loads((p/'transferred_weights.json').read_text())['weights']
b=np.array(d['bodyVertices']);c=np.array(d['clothVertices']);bt=np.array(d['bodyTriangles']).reshape(-1,3);ct=np.array(d['clothTriangles']).reshape(-1,3)
def normals(b):
 n=np.cross(b[bt[:,1]]-b[bt[:,0]],b[bt[:,2]]-b[bt[:,0]]);out=np.zeros_like(b)
 for j in range(3):np.add.at(out,bt[:,j],n)
 return out/np.maximum(1e-12,np.linalg.norm(out,axis=1)[:,None])
def nearest(b,bvh,v,n):
 pos,normal,i,dist=bvh.find_nearest(Vector(v));tri=bt[i];a,x,y=b[tri];uv=np.linalg.lstsq(np.column_stack((x-a,y-a)),np.array(pos)-a,rcond=None)[0];bary=np.maximum(0,[1-uv.sum(),*uv]);bary/=sum(bary);normal=(n[tri]*bary[:,None]).sum(0);normal/=max(np.linalg.norm(normal),1e-12)
 return np.array(pos),normal,dist
bvh=BVHTree.FromPolygons(b.tolist(),bt.tolist(),all_triangles=True);n=normals(b)
direction=np.array([nearest(b,bvh,v,n)[1] for v in c]);amount=np.zeros(len(c))
prepared=[]
for s in samples:
 body=np.array(s['body']);mat=np.array(s['matrices']).reshape(-1,3,3);linear=np.array([sum(mat[i]*w for i,w in row) for row in weights]);prepared.append((s,body,BVHTree.FromPolygons(body.tolist(),bt.tolist(),all_triangles=True),normals(body),linear))
for iteration in range(2):
 for s,body,tree,norm,linear in prepared:
  posed=np.array(s['after']);dirs=np.einsum('nij,nj->ni',linear,direction)
  for i,v in enumerate(posed+dirs*amount[:,None]):
   pos,normal,dist=nearest(body,tree,v,norm);signed=np.dot(v-pos,normal);alignment=np.dot(dirs[i],normal)
   if signed<.004 and dist<.04 and alignment>.25:amount[i]=min(.025,amount[i]+(.004-signed)/alignment)
 # Feather the fit correction across adjacent triangles without lowering the envelope.
 edges=np.concatenate([ct[:,[0,1]],ct[:,[1,2]],ct[:,[2,0]]]);edges=np.concatenate([edges,edges[:,::-1]])
 for _ in range(4):
  total=np.zeros(len(c));count=np.zeros(len(c));np.add.at(total,edges[:,0],amount[edges[:,1]]);np.add.at(count,edges[:,0],1);amount=np.maximum(amount,total/np.maximum(count,1)*.85)
 groups={}
 for i,v in enumerate(c):groups.setdefault(tuple(np.round(v,5)),[]).append(i)
 for g in groups.values():amount[g]=max(amount[g]);direction[g]=direction[g].mean(0)
 print('iteration',iteration,'max',amount.max(),'95',np.percentile(amount,95),flush=True)
delta=direction*amount[:,None]
(p/'fit_offsets.json').write_text(json.dumps(delta.tolist()))
stats=[]
for s,body,tree,norm,linear in prepared:
 for label,vertices in [('before',np.array(s['after'])),('after',np.array(s['after'])+np.einsum('nij,nj->ni',linear,delta))]:
  signed=np.array([np.dot(v-pos,n) for v in vertices for pos,n,dist in [nearest(body,tree,v,norm)]])
  stats.append({'pose':s['name'],'fraction':s['fraction'],'variant':label,'insideOver1mm':int(sum(signed<-.001)),'penetrationSumM':float(-np.minimum(signed,0).sum()),'worstM':float(signed.min())})
(p/'fit_verification.json').write_text(json.dumps({'maxOffsetM':float(amount.max()),'offset95M':float(np.percentile(amount,95)),'samples':stats},indent=2));print('total before',sum(x['insideOver1mm'] for x in stats if x['variant']=='before'),'after',sum(x['insideOver1mm'] for x in stats if x['variant']=='after'))
