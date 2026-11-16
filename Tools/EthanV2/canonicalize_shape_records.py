"""Reorder sparse blendshape entries only; retain every value and frame range."""
from pathlib import Path
import hashlib
import json

root=Path('Data/EthanV2/SmileRepair')
source=Path('Assets/_Sandbox/Characters/Ethan_V2/Meshes/EthanV2_Head_FacialHairOptimized.asset')
counts=[]
with source.open(newline='') as f:
    for line in f:
        if line.strip()=='shapes:':break
    for line in f:
        if line.strip()=='channels:':break
        if 'vertexCount:' in line:counts.append(int(line.split(':')[1]))
assert len(counts)==73
changed=0
with source.open(newline='') as src,(root/'canonical_head.asset').open('w',newline='') as dst:
    for line in src:
        dst.write(line)
        if line.strip()=='vertices:':break
    for count in counts:
        records=[tuple(next(src) for _ in range(4)) for _ in range(count)]
        assert all(r[0].lstrip().startswith('- vertex:') and r[3].lstrip().startswith('index:') for r in records)
        ordered=sorted(records,key=lambda r:int(r[3].split(':')[1]))
        assert len({int(r[3].split(':')[1]) for r in ordered})==count
        changed+=records!=ordered
        # Complete record multiset equality proves position/normal/tangent/index preservation.
        assert sorted(records)==sorted(ordered)
        for record in ordered:dst.writelines(record)
    for line in src:dst.write(line)
report={'frames':len(counts),'framesReordered':changed,'records':sum(counts),'allRecordValuesPreservedExactly':True}
(root/'canonicalization.json').write_text(json.dumps(report,indent=2))
print(report)
