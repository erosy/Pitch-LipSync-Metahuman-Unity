"""Compare isolated Unity import with Blender meter-space baselines."""
import hashlib
import json
from pathlib import Path

import numpy as np
from mathutils.kdtree import KDTree

OUT=Path(__file__).resolve().parents[2]/'Data/JiwooReimport/Batch03'


def xyz(v):
    return np.array([v['x'],v['y'],v['z']],dtype=float)


def main():
    source=json.loads((OUT/'export_report.json').read_text())
    unity=json.loads((OUT/'unity_import_report.json').read_text())
    bones={b['name']: b for b in unity['bones']}
    names=sorted(source['bones'].keys() & bones.keys())
    src=np.array([source['bones'][n] for n in names])
    dst=np.array([xyz(bones[n]['position']) for n in names])
    # Fit coordinate-system conversion from 1,185 independently imported joints.
    design=np.c_[src,np.ones(len(src))]
    conversion=np.linalg.lstsq(design,dst,rcond=None)[0]
    bone_error=float(np.max(np.linalg.norm(design@conversion-dst,axis=1)))
    points=np.load(OUT/'hair_head_local_baseline.npy',mmap_mode='r').reshape(-1,3)
    head_source=np.array(source['head_matrix_blender'])
    head_unity=np.array(bones['head']['matrix']).reshape(4,4)
    world_conversion=np.eye(4)
    world_conversion[:3,:]=conversion.T
    head_conversion=np.linalg.inv(head_unity)@world_conversion@head_source
    errors=[]
    for sample in unity['hairSamples']:
        expected=(head_conversion@np.r_[points[sample['index']],1])[:3]
        errors.append(float(np.linalg.norm(expected-xyz(sample['position']))))
    base=np.load(OUT/'character_baseline.npz')['mesh']
    expected_vertices=np.c_[base,np.ones(len(base))]@conversion
    tree=KDTree(len(expected_vertices))
    for i,p in enumerate(expected_vertices): tree.insert(p,i)
    tree.balance()
    actual_vertices=np.fromfile(OUT/'unity_vertices.bin',dtype=np.float32).reshape(-1,3)
    mesh_error=max(tree.find(p)[2] for p in actual_vertices)
    correct_shapes=set(unity['blendShapes'])==set(source['shape_names'])
    width_ok=abs(unity['widthMin']-0.0001)<1e-8 and abs(unity['widthMax']-0.0001)<1e-8
    scale_error=max(np.max(np.abs(xyz(unity[k])-1)) for k in ('headScale','rootScale'))
    result={
        'unity_version':unity['unityVersion'],
        'avatar_valid':unity['avatarValid'],'avatar_humanoid':unity['avatarHuman'],
        'matching_bone_count':len(names),'bone_world_max_error_m':bone_error,
        'world_conversion':world_conversion.tolist(),
        'head_local_conversion':head_conversion.tolist(),
        'root_scale':unity['rootScale'],'head_scale':unity['headScale'],
        'blendshape_names_preserved':correct_shapes,'blendshape_count':len(unity['blendShapes']),
        'mesh_vertex_surface_max_error_m':float(mesh_error),
        'curve_count':unity['curveCount'],'hair_points':unity['hairPoints'],
        'hair_alignment_samples':len(errors),'hair_head_local_max_error_m':max(errors),
        'width_min_m':unity['widthMin'],'width_max_m':unity['widthMax'],
        'fbx_scale_factor':1,'fbx_bake_axis_conversion':True,
        'abc_scale_factor':1,'abc_swap_handedness':True,
        'hair_instance_transform':'position 0, rotation 0, scale 1 under head',
        'strand_scale':'Fixed',
        'simulation_verified':False,
    }
    passed=(unity['avatarValid'] and unity['avatarHuman'] and scale_error<1e-4 and
            correct_shapes and bone_error<1e-4 and mesh_error<1e-4 and
            max(errors)<1e-4 and unity['curveCount']==source['curve_count'] and width_ok)
    result['status']='PASS' if passed else 'FAIL'
    (OUT/'unity_verification_report.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print('UNITY_VERIFICATION '+json.dumps(result),flush=True)
    assert passed, 'Unity import comparison failed; inspect report.'


main()
