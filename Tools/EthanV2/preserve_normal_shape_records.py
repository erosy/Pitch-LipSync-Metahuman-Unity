"""Stage corrected shading while preserving every original sparse position delta.

Writes outside Assets; Unity's exact geometry guard must pass before installation.
"""
from pathlib import Path
import json
import re
import numpy as np

root = Path('Data/EthanV2/NormalRepair')
original = root / 'before_head.asset'
frames = []
with original.open() as f:
    for line in f:
        if line == '    shapes:\n':
            break
    for line in f:
        if line == '    channels:\n':
            break
        if 'firstVertex:' in line:
            start = int(line.split(':')[1])
        elif 'vertexCount:' in line:
            frames.append((start, int(line.split(':')[1])))
assert len(frames) == 73
shading = np.memmap(root / 'corrected_shading.bin', dtype='<f4', mode='r', shape=(73, 104632, 6))

def vector(label, values, indent):
    return indent + label + ': {x: %.9g, y: %.9g, z: %.9g}\n' % tuple(values)

counts = []
with original.open() as src, (root / 'corrected_head.asset').open('w', newline='') as dst:
    for line in src:
        dst.write(line)
        if line == '    vertices:\n':
            break
    for frame, (_, count) in enumerate(frames):
        seen = set()
        records = {}
        for _ in range(count):
            position, normal, tangent, indexline = (next(src) for _ in range(4))
            assert position.startswith('    - vertex:')
            index = int(indexline.split(':')[1])
            assert index not in seen
            seen.add(index)
            records[index] = (position,
                vector('normal', shading[frame, index, :3], '      '),
                vector('tangent', shading[frame, index, 3:], '      '), indexline)
        extra = np.flatnonzero(np.any(shading[frame] != 0, axis=1))
        for index in extra:
            index = int(index)
            if index in seen:
                continue
            seen.add(index)
            records[index] = ('    - vertex: {x: 0, y: 0, z: 0}\n',
                vector('normal', shading[frame, index, :3], '      '),
                vector('tangent', shading[frame, index, 3:], '      '),
                f'      index: {index}\n')
        # Native mesh/GPU processing requires canonical order inside each frame.
        for index in sorted(records):
            dst.writelines(records[index])
        counts.append(len(seen))
    assert next(src) == '    shapes:\n'
    dst.write('    shapes:\n')
    offset = 0
    for count in counts:
        first, oldcount, normals, tangents = (next(src) for _ in range(4))
        assert 'firstVertex:' in first and 'vertexCount:' in oldcount
        dst.write(f'    - firstVertex: {offset}\n      vertexCount: {count}\n')
        dst.write(normals)
        dst.write(tangents)
        offset += count
    for line in src:
        dst.write(line)
(root / 'sparse_record_preservation.json').write_text(json.dumps({
    'originalRecords': sum(c for _, c in frames),
    'correctedRecords': sum(counts),
    'originalPositionRecordsPreserved': True,
    'sparseIndicesStrictlyAscendingPerFrame': True,
    'productionWritten': False,
}, indent=2))
print(json.loads((root / 'sparse_record_preservation.json').read_text()))
