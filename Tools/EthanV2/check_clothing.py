import json,numpy as np
from pathlib import Path
from mathutils.bvhtree import BVHTree
from mathutils import Vector
p=Path('Data/EthanV2/ClothingRepair'); d=json.loads((p/'binding_input.json').read_text());samples=json.loads((p/'pose_samples.json').read_text());anchors=json.loads((p/'transferred_weights.json').read_text())['anchors']
bt=np.array(d['bodyTriangles']).reshape(-1,3);ct=np.array(d['clothTriangles']).reshape(-1,3);rest=np.array(d['clothVertices']);tri=np.array([a['triangle'] for a in anchors]);bar=np.array([a['bary'] for a in anchors]);br=np.array(d['bodyVertices']);ar=(br[tri]*bar[:,:,None]).sum(axis=1);baseline=np.linalg.norm(rest-ar,axis=1)
edges=np.unique(np.sort(np.concatenate([ct[:,[0,1]],ct[:,[1,2]],ct[:,[2,0]]]),axis=1),axis=0);length=np.linalg.norm(rest[edges[:,0]]-rest[edges[:,1]],axis=1);edges=edges[length>.001];length=length[length>.001]
groups={}
for i,v in enumerate(rest):groups.setdefault(tuple(np.round(v,5)),[]).append(i)
groups=[g for g in groups.values() if len(g)>1]
out=[]
for s in samples:
 b=np.array(s['body']);anchor=(b[tri]*bar[:,:,None]).sum(axis=1);stats={}
 for key in ['before','after']:
  c=np.array(s[key]);change=np.abs(np.linalg.norm(c-anchor,axis=1)-baseline);ratio=np.linalg.norm(c[edges[:,0]]-c[edges[:,1]],axis=1)/length
  stats[key]={'anchorDistanceChange95M':float(np.percentile(change,95)),'edgeStretch99':float(np.percentile(ratio,99)),'maxSeamGapM':max(float(np.max(np.linalg.norm(c[g]-c[g[0]],axis=1))) for g in groups)}
 out.append({'name':s['name'],'fraction':s['fraction'],**stats})
(p/'verification.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
