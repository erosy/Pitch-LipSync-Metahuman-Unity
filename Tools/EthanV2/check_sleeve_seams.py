import json,numpy as np
from scipy.spatial import cKDTree
from pathlib import Path
p=Path('Data/EthanV2/ClothingRepair');d=json.loads((p/'binding_input.json').read_text());v=np.array(d['clothVertices']);t=np.array(d['clothTriangles']).reshape(-1,3)
rounded=np.round(v,6);_,first,inv=np.unique(rounded,axis=0,return_index=True,return_inverse=True);wt=inv[t];edges=np.concatenate([wt[:,[0,1]],wt[:,[1,2]],wt[:,[2,0]]]);edges=np.sort(edges,axis=1);u,n=np.unique(edges,axis=0,return_counts=True);boundary=np.unique(u[n==1]);points=v[first[boundary]];tree=cKDTree(points);pairs=tree.query_pairs(.005)
print('original vertices',len(v),'welded',len(first),'boundary points',len(boundary),'pairs within5mm',len(pairs))
samples=[np.array(s['after']) for s in json.loads((p/'pose_samples.json').read_text())];rows=[]
for a,b in pairs:
 i,j=first[boundary[a]],first[boundary[b]];dist=np.linalg.norm(v[i]-v[j]);motion=max(np.linalg.norm(s[i]-s[j]) for s in samples)
 if motion>dist+.003:rows.append({'i':int(i),'j':int(j),'rest':dist,'max':motion,'position':v[i].tolist()})
rows.sort(key=lambda r:-r['max']);print(json.dumps(rows[:20],indent=2));(p/'seam_candidates.json').write_text(json.dumps(rows))
