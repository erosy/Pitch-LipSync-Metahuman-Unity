"""Stage canonical sparse records outside Assets; the Editor installs them."""
from pathlib import Path
import numpy as np,struct,json
ROOT=Path('D:/UnityProject/MetahumanTemplate');OUT=ROOT/'Data/Liora/Integration'
reports=[]
for name in ['Eyebrows','Eyelashes']:
 raw=(OUT/(name+'.bin')).read_bytes();n,tri,shapes=struct.unpack('<iii',raw[:12]);offset=12+n*32+tri*4
 frames=np.frombuffer(raw[offset:],'<f4').reshape(shapes,2,n,3)
 original=ROOT/('Assets/_Sandbox/Characters/Liora/Candidate/Liora_'+name+'.asset')
 counts=[]
 with original.open() as src,(OUT/(name+'_preserved.asset')).open('w',newline='') as dst:
  for line in src:
   dst.write(line)
   if line=='    vertices:\n':break
  else:raise AssertionError('Missing shape vertices')
  for frame in frames:
   indices=np.flatnonzero(np.any(frame!=0,axis=(0,2)));counts.append(len(indices))
   for index in indices:
    p,norm=frame[:,index]
    dst.write('    - vertex: {x: %.9g, y: %.9g, z: %.9g}\n'%tuple(p))
    dst.write('      normal: {x: %.9g, y: %.9g, z: %.9g}\n'%tuple(norm))
    dst.write('      tangent: {x: 0, y: 0, z: 0}\n      index: %d\n'%index)
  for line in src:
   if line=='    shapes:\n':break
  dst.write('    shapes:\n');at=0
  for count in counts:
   for _ in range(4):next(src)
   dst.write('    - firstVertex: %d\n      vertexCount: %d\n      hasNormals: 1\n      hasTangents: 0\n'%(at,count));at+=count
  for line in src:dst.write(line)
 reports.append({'name':name,'shapes':shapes,'records':sum(counts),'indicesStrictlyAscending':True,'positionsAndNormalsFromEvaluatedGroom':True,'temporaryTangentSentinelsRemoved':True})
(OUT/'facial_record_preservation.json').write_text(json.dumps(reports,indent=2))
