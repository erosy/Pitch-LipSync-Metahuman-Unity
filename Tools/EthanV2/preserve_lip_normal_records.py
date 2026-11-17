"""Stage lip-only expression shading, preserving every original position record."""
from pathlib import Path
import json
import numpy as np

root = Path('Data/EthanV2/LipNormalRepair')
original = root / 'before_face.asset'
start, size, shapes = 139924, 4252, 73
shading = np.memmap(root / 'shading.bin', dtype='<f4', mode='r', shape=(shapes, size, 6))
counts = []
with original.open() as src:
    for line in src:
        if line == '    shapes:\n': break
    for line in src:
        if line == '    channels:\n': break
        if 'vertexCount:' in line: counts.append(int(line.split(':')[1]))
assert len(counts) == shapes

def vector(label, v):
    return '      %s: {x: %.9g, y: %.9g, z: %.9g}\n' % (label, *v)

newcounts = []
with original.open() as src, (root / 'corrected_face.asset').open('w', newline='') as dst:
    for line in src:
        dst.write(line)
        if line == '    vertices:\n': break
    for shape, count in enumerate(counts):
        records = {}
        for _ in range(count):
            record = tuple(next(src) for _ in range(4))
            assert record[0].startswith('    - vertex:')
            i = int(record[3].split(':')[1])
            assert i not in records
            if start <= i < start + size:
                v = shading[shape, i-start]
                record = (record[0], vector('normal', v[:3]), vector('tangent', v[3:]), record[3])
            records[i] = record
        for j in np.flatnonzero(np.any(shading[shape] != 0, axis=1)):
            i = start + int(j)
            if i not in records:
                v = shading[shape, j]
                records[i] = ('    - vertex: {x: 0, y: 0, z: 0}\n', vector('normal', v[:3]), vector('tangent', v[3:]), f'      index: {i}\n')
        for i in sorted(records): dst.writelines(records[i])
        newcounts.append(len(records))
    assert next(src) == '    shapes:\n'
    dst.write('    shapes:\n')
    offset = 0
    for count in newcounts:
        first, oldcount, normal, tangent = (next(src) for _ in range(4))
        assert 'firstVertex:' in first and 'vertexCount:' in oldcount
        dst.write(f'    - firstVertex: {offset}\n      vertexCount: {count}\n')
        dst.write(normal); dst.write(tangent)
        offset += count
    for line in src: dst.write(line)
report = dict(originalRecords=sum(counts), correctedRecords=sum(newcounts), sortedIndices=True, originalPositionRecordsPreserved=True, shadingRange=[start, start+size], productionWritten=False)
(root / 'sparse_preservation.json').write_text(json.dumps(report, indent=2))
print(report)
