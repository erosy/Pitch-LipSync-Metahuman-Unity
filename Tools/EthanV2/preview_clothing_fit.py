import json,numpy as np
from pathlib import Path
p=Path('Data/EthanV2/ClothingRepair');samples=json.loads((p/'pose_samples.json').read_text());delta=np.array(json.loads((p/'fit_offsets.json').read_text()));weights=json.loads((p/'transferred_weights.json').read_text())['weights']
for s in samples:
 mat=np.array(s['matrices']).reshape(-1,3,3);linear=np.array([sum(mat[i]*w for i,w in row) for row in weights]);s['before']=s['after'];s['after']=(np.array(s['after'])+np.einsum('nij,nj->ni',linear,delta)).tolist()
code=Path('Tools/EthanV2/render_clothing_check.py').read_text();code=code.replace("s=json.loads((p/'pose_samples.json').read_text())","s=samples");code=code.replace('[s[4],s[10],s[13]]','[s[8],s[20],s[26]]');code='\n'.join(line for line in code.splitlines() if "if key=='after'" not in line);code=code.replace('comparison.png','comparison_fitted.png');exec(code)
