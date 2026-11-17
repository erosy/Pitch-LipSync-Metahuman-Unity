"""Preserve the corrected production head's complete sparse records in merged Face.

The head is section zero, in unchanged coordinates and vertex order. This avoids
Mesh.AddBlendShapeFrame dropping tiny position-only entries. Output is staged in
Data; the Editor imports only the candidate asset after verification.
"""
from pathlib import Path
import json

root=Path('Data/EthanV2/Revamp')
sections=json.loads((root/'sections.json').read_text())
head=Path(sections[0]['source'])
merged=Path('Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Face.asset')
head_count=sections[0]['vertices']
audit=json.loads((root/'body_channel_audit.json').read_text())
assert audit['bodyChannelsInactive'] and audit['maxPositionDelta']==0 and audit['maxNormalDelta']==0
body_end=head_count+sections[1]['vertices']

def frame_counts(path):
    result=[]
    with path.open(newline='') as f:
        for line in f:
            if line.strip()=='shapes:':break
        for line in f:
            if line.strip()=='channels:':break
            if 'vertexCount:' in line:result.append(int(line.split(':')[1]))
    assert len(result)==73
    return result

hc,mc=frame_counts(head),frame_counts(merged)
counts=[]
with head.open(newline='') as h,merged.open(newline='') as m,(root/'Face_preserved.asset').open('w',newline='') as out:
    for line in h:
        if line.strip()=='vertices:':break
    for line in m:
        out.write(line)
        if line.strip()=='vertices:':break
    for a,b in zip(hc,mc):
        records={}
        for _ in range(a):
            record=tuple(next(h) for _ in range(4))
            assert record[0].lstrip().startswith('- vertex:')
            index=int(record[3].split(':')[1]);assert index<head_count
            records[index]=record
        for _ in range(b):
            record=tuple(next(m) for _ in range(4))
            assert record[0].lstrip().startswith('- vertex:')
            index=int(record[3].split(':')[1])
            if index>=body_end:records[index]=record
        for index in sorted(records):out.writelines(records[index])
        counts.append(len(records))
    assert next(m).strip()=='shapes:'
    out.write('    shapes:\n')
    offset=0
    for count in counts:
        first,old,normals,tangents=(next(m) for _ in range(4))
        assert 'firstVertex:' in first and 'vertexCount:' in old
        out.write(f'    - firstVertex: {offset}\n      vertexCount: {count}\n')
        out.write(normals);out.write(tangents);offset+=count
    for line in m:out.write(line)
report={'shapes':73,'headVertices':head_count,'sourceHeadRecords':sum(hc),'mergedRecords':sum(counts),'headRecordsCopiedVerbatim':True,'indicesSorted':True,'inactiveBodyTangentRecordsRemoved':True,'previousMergedRecords':sum(mc)}
(root/'head_record_preservation.json').write_text(json.dumps(report,indent=2))
print(report)
