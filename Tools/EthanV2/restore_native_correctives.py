"""Restore DNA corrective deltas on the delivery's split/reindexed face meshes.

Every target is sampled from the original DNA; nearest rest-position mapping is
used only where the modeller split/reindexed the head. No rig or mesh is rebuilt.
"""
import bpy, importlib, pathlib, json, shutil, hashlib, numpy as np
from mathutils.kdtree import KDTree
ROOT=pathlib.Path('D:/UnityProject/MetahumanTemplate');OUT=ROOT/'Data/EthanV2/Native'
A='bl_ext.api_portal_polyhammer_com_001.character_dna'
u=importlib.import_module(A+'.utilities');controller=importlib.import_module(A+'.runtime.controller');engine=importlib.import_module(A+'.runtime.engine')
inst=u.get_addon_scene_properties().rig_instance_list[0];inst.initialize();controller.rebuild(inst)
r=inst.head_dna_reader
def coords(o):
    a=np.empty(len(o.data.vertices)*3,dtype=np.float32);o.data.vertices.foreach_get('co',a);return a.reshape(-1,3)
report={'method':'Original DNA deltas with nearest neutral vertex matching for split head; original mesh positions and weights retained.','meshes':[]}
prefix=inst.name+'_'
controller.suspend(inst)
for idx in r.getMeshIndicesForLOD(0):
    meshname=r.getMeshName(idx)
    points=np.array([r.getVertexPositionXs(idx),r.getVertexPositionYs(idx),r.getVertexPositionZs(idx)],dtype=np.float32).T*.01
    kd=KDTree(len(points))
    for j,v in enumerate(points):kd.insert(v,j)
    kd.balance()
    main=bpy.data.objects.get(prefix+meshname)
    targets=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith(prefix+meshname)]
    for obj in sorted(targets,key=lambda o:o.name):
        raw=coords(obj)
        same_order=bool(len(raw)==len(points) and np.max(np.linalg.norm(raw-points,axis=1))<1e-5)
        matches=[(None,i,float(np.linalg.norm(v-points[i]))) for i,v in enumerate(raw)] if same_order else [kd.find(v) for v in raw]
        indices=np.array([v[1] for v in matches],dtype=np.int32);dist=np.array([v[2] for v in matches])
        assert dist.max()<.0005,(obj.name,float(dist.max()))
        assert not obj.data.shape_keys,obj.name+': unexpected existing shape keys'
        count=r.getBlendShapeTargetCount(idx);restored=0
        if count:obj.shape_key_add(name='Basis',from_mix=False)
        for target in range(count):
            channel=r.getBlendShapeChannelIndex(idx,target)
            name=meshname+'__'+r.getBlendShapeChannelName(channel)
            delta=np.zeros_like(points)
            vi=np.array(r.getBlendShapeTargetVertexIndices(idx,target),dtype=np.int64)
            delta[vi]=np.array([r.getBlendShapeTargetDeltaXs(idx,target),r.getBlendShapeTargetDeltaYs(idx,target),r.getBlendShapeTargetDeltaZs(idx,target)],dtype=np.float32).T*.01
            mapped=delta[indices]
            if obj!=main and np.max(np.abs(mapped))<1e-10:continue
            key=obj.shape_key_add(name=name,from_mix=False)
            key.data.foreach_set('co',(raw+mapped).ravel());key.value=0
            if obj!=main:
                driver=key.driver_add('value').driver;driver.type='SUM'
                variable=driver.variables.new();variable.name='native_value';variable.type='SINGLE_PROP'
                variable.targets[0].id_type='KEY';variable.targets[0].id=main.data.shape_keys
                variable.targets[0].data_path='key_blocks['+json.dumps(name)+'].value'
            restored+=1
        obj.data.update()
        report['meshes'].append({'object':obj.name,'dna_mesh':meshname,'restored_targets':restored,'same_vertex_order':same_order,'max_neutral_vertex_match_distance_m':float(dist.max()),'p99_neutral_vertex_match_distance_m':float(np.quantile(dist,.99)),'vertices':len(raw),'weights_changed':False})
        print('CORRECTIVES_RESTORED',obj.name,restored,'match_mm',float(dist.max())*1000,flush=True)
# Recreate native output bindings after the targets exist, and release authoring suspension.
controller.resume(inst,True)
inst.initialize();controller.rebuild(inst)
report['binding_issues']=engine.binding_issues(inst)
assert not report['binding_issues']
deps=OUT/'Dependencies';deps.mkdir(exist_ok=True)
for component in ['head','body']:
    src=pathlib.Path(bpy.path.abspath(getattr(inst,component+'_dna_file_path')))
    dest=deps/(component+'.dna')
    if not dest.exists():shutil.copyfile(src,dest)
    assert hashlib.sha256(dest.read_bytes()).digest()==hashlib.sha256(src.read_bytes()).digest()
    setattr(inst,component+'_dna_file_path','//Dependencies/'+component+'.dna')
for o in engine.carriers(inst):
    o['head_dna_file_path']='//Dependencies/head.dna';o['body_dna_file_path']='//Dependencies/body.dna'
bpy.context.view_layer.update()
(OUT/'corrective_restoration.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ethan_V2_FaceBoard_Restored.blend'))
print('RESTORED_SOURCE_SAVED',flush=True)
