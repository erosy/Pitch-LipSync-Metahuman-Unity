"""Stage scoped shading changes, preserving every original position record."""
from pathlib import Path
import json,struct,numpy as np
root=Path('D:/UnityProject/MetahumanTemplate/Data/Liora/ShadingRepair')
preparation=json.loads((root/'preparation.json').read_text())
with (root/'corrected_shading.bin').open('rb') as f:shapes,count=struct.unpack('<ii',f.read(8))
shading=np.memmap(root/'corrected_shading.bin',dtype='<f4',offset=8,shape=(shapes,count,6))
mapping=np.full(preparation['vertexCount'],-1,np.int32);at=0
for section in preparation['ranges']:
 start,n=section['start'],section['count'];mapping[start:start+n]=np.arange(at,at+n);at+=n
assert at==count
oldcounts=[]
with (root/'before_face.asset').open() as src:
 for line in src:
  if line=='    shapes:\n':break
 for line in src:
  if line=='    channels:\n':break
  if 'vertexCount:' in line:oldcounts.append(int(line.split(':')[1]))
assert len(oldcounts)==shapes
def vector(label,v):return '      %s: {x: %.9g, y: %.9g, z: %.9g}\n'%(label,*v)
counts=[];added=0;changed=0
with (root/'before_face.asset').open() as src,(root/'corrected_face.asset').open('w',newline='') as dst:
 for line in src:
  dst.write(line)
  if line=='    vertices:\n':break
 for shape,oldcount in enumerate(oldcounts):
  records={}
  for _ in range(oldcount):
   record=tuple(next(src) for _ in range(4));assert record[0].startswith('    - vertex:')
   index=int(record[3].split(':')[1]);assert index not in records
   local=mapping[index]
   if local>=0:
    value=shading[shape,local];record=(record[0],vector('normal',value[:3]),vector('tangent',value[3:]),record[3]);changed+=1
   records[index]=record
  for index in np.flatnonzero(mapping>=0):
   if index not in records:
    value=shading[shape,mapping[index]]
    if np.any(value!=0):records[int(index)]=('    - vertex: {x: 0, y: 0, z: 0}\n',vector('normal',value[:3]),vector('tangent',value[3:]),'      index: %d\n'%index);added+=1
  for index in sorted(records):dst.writelines(records[index])
  counts.append(len(records))
  if shape%12==0:print('SCOPED_SHADING_FRAME',shape,flush=True)
 assert next(src)=='    shapes:\n';dst.write('    shapes:\n');offset=0
 for count in counts:
  first,old,normals,tangents=(next(src) for _ in range(4));assert 'firstVertex:' in first and 'vertexCount:' in old
  dst.write('    - firstVertex: %d\n      vertexCount: %d\n'%(offset,count));dst.write(normals);dst.write(tangents);offset+=count
 for line in src:dst.write(line)
report={'originalRecords':sum(oldcounts),'correctedRecords':sum(counts),'scopedRecordsUpdated':changed,'shadingOnlyRecordsAdded':added,'originalPositionRecordsPreservedVerbatim':True,'allUntouchedRecordsPreservedVerbatim':True,'sparseIndicesStrictlyAscending':True,'channelDefinitionsAndFrameWeightsPreserved':True}
(root/'sparse_preservation.json').write_text(json.dumps(report,indent=2));print('SCOPED_SHADING_PRESERVED',report,flush=True)
