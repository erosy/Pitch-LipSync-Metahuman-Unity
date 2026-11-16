import json,numpy as np,cv2
from pathlib import Path
p=Path('Data/EthanV2/ClothingRepair');d=json.loads((p/'binding_input.json').read_text());s=json.loads((p/'pose_samples.json').read_text());bt=np.array(d['bodyTriangles']).reshape(-1,3);ct=np.array(d['clothTriangles']).reshape(-1,3)
panels=[]
for sample in [s[4],s[10],s[13]]:
 for key in ['before','after']:
  canvas=np.full((800,640,3),235,np.uint8);faces=[]
  for vertices,tri,color in [(sample['body'],bt,(149,179,214)),(sample[key],ct,(75,75,75))]:
   if key=='after' and len(tri)==len(bt):tri=tri[np.setdiff1d(np.arange(len(tri)),json.loads((p/'body_mask.json').read_text())['removeTriangles'])]
   v=np.array(vertices);a=v[tri];n=np.cross(a[:,1]-a[:,0],a[:,2]-a[:,0]);n/=np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-12);shade=.65+.35*np.abs(n[:,2]);xy=np.stack([320-a[:,:,0]*650,1100-a[:,:,1]*650],axis=2).astype(np.int32)
   for z,poly,light in zip(a[:,:,2].mean(axis=1),xy,shade):faces.append((z,poly,tuple(int(x*light) for x in color)))
  for _,poly,color in sorted(faces,key=lambda x:x[0]):cv2.fillConvexPoly(canvas,poly,color)
  cv2.putText(canvas,sample['name']+' '+key,(15,30),cv2.FONT_HERSHEY_SIMPLEX,.65,(20,20,20),2);panels.append(canvas)
cv2.imwrite(str(p/'comparison.png'),np.concatenate(panels,axis=1))
