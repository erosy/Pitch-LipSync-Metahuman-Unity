import json,numpy as np
from pathlib import Path
p=Path('Data/EthanV2/ClothingRepair');d=json.loads((p/'binding_input.json').read_text());samples=json.loads((p/'pose_samples.json').read_text());anchors=json.loads((p/'transferred_weights.json').read_text())['anchors'];b=np.array(d['bodyVertices']);c=np.array(d['clothVertices']);tri=np.array([a['triangle'] for a in anchors]);bary=np.array([a['bary'] for a in anchors]);
def frame(body):
 a=body[tri];t=a[:,1]-a[:,0];t/=np.maximum(np.linalg.norm(t,axis=1),1e-12)[:,None];n=np.cross(t,a[:,2]-a[:,0]);n/=np.maximum(np.linalg.norm(n,axis=1),1e-12)[:,None];y=np.cross(n,t);return np.stack([t,y,n],axis=2)
restframe=frame(b);positions=(b[tri]*bary[:,:,None]).sum(axis=1);offset=np.einsum('nji,nj->ni',restframe,c-positions);offset[:,2]=np.maximum(offset[:,2],.006)+.015
(p/'surface_bindings.json').write_text(json.dumps({'triangles':tri.tolist(),'bary':bary.tolist(),'offsets':offset.tolist()}))
for s in samples:
 body=np.array(s['body']);s['before']=s['after'];s['after']=((body[tri]*bary[:,:,None]).sum(axis=1)+np.einsum('nij,nj->ni',frame(body),offset)).tolist()
code=Path('Tools/EthanV2/render_clothing_check.py').read_text().replace("s=json.loads((p/'pose_samples.json').read_text())","s=samples").replace('[s[4],s[10],s[13]]','[s[8],s[20],s[26]]');code='\n'.join(line for line in code.splitlines() if "if key=='after'" not in line);code=code.replace('comparison.png','comparison_surface.png');exec(code)
