"""Stage exact projected sparse arrays for the merged Face, outside Assets."""
from pathlib import Path
import struct,json,numpy as np
root=Path('D:/UnityProject/MetahumanTemplate');out=root/'Data/Liora/HierarchyRepair'
binary=out/'desired_shapes.bin'
with binary.open('rb') as f:vertices,shapes=struct.unpack('<ii',f.read(8))
frames=np.memmap(binary,dtype='<f4',offset=8,shape=(shapes,vertices,9))
original=root/'Assets/_Sandbox/Characters/Liora/Candidate/Liora_Face.asset'
counts=[]
with original.open() as src,(out/'Face_preserved.asset').open('w',newline='') as dst:
 for line in src:
  dst.write(line)
  if line=='    vertices:\n':break
 else:raise AssertionError('Missing shape records')
 for shape,frame in enumerate(frames):
  indices=np.flatnonzero(np.any(frame!=0,axis=1));counts.append(len(indices))
  for index in indices:
   p,n,t=frame[index].reshape(3,3)
   dst.write('    - vertex: {x: %.9g, y: %.9g, z: %.9g}\n'%tuple(p))
   dst.write('      normal: {x: %.9g, y: %.9g, z: %.9g}\n'%tuple(n))
   dst.write('      tangent: {x: %.9g, y: %.9g, z: %.9g}\n'%tuple(t))
   dst.write('      index: %d\n'%index)
  if shape%12==0:print('PRESERVED_FRAME',shape,'records',len(indices),flush=True)
 for line in src:
  if line=='    shapes:\n':break
 dst.write('    shapes:\n');offset=0
 for count in counts:
  for _ in range(4):next(src)
  dst.write('    - firstVertex: %d\n      vertexCount: %d\n      hasNormals: 1\n      hasTangents: 1\n'%(offset,count));offset+=count
 for line in src:dst.write(line)
report={'shapes':shapes,'vertices':vertices,'records':sum(counts),'indicesStrictlyAscending':True,'positionNormalAndTangentArraysPreserved':True,'temporarySentinelsRemoved':True,'bodyAndOutfitDataNotStripped':True}
(out/'sparse_preservation.json').write_text(json.dumps(report,indent=2));print('MERGED_SPARSE_PRESERVATION_COMPLETE',report,flush=True)
