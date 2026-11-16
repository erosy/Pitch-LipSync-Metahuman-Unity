import bpy,numpy as np,pathlib,json
ROOT=pathlib.Path('D:/UnityProject/MetahumanTemplate');OUT=ROOT/'Data/EthanV2/Eyebrows'
for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(OUT/'Ethan_V2_73_WithEyebrows.fbx'))
head=bpy.data.objects['BAKED_head_lod0_mesh'];report=json.loads((OUT/'export_report.json').read_text());expected=np.load(OUT/'brow_baselines.npz')
assert len(head.data.vertices)==report['head_vertices_after']
assert [k.name for k in head.data.shape_keys.key_blocks][1:]==report['shape_names']
start=report['head_vertices_before'];maximum=0
for key in head.data.shape_keys.key_blocks:
 a=np.empty(len(key.data)*3,np.float32);key.data.foreach_get('co',a);a=a.reshape(-1,3)[start:]
 error=float(np.linalg.norm(a-expected[key.name],axis=1).max());maximum=max(maximum,error);assert error<3e-6,(key.name,error)
meshes=[o for o in bpy.data.objects if o.type=='MESH'];assert len(meshes)==13
assert not any(o.name.startswith(('WGT','CTRL','Ethan_NewMetaHumanCharacter_')) for o in meshes)
manifest=json.loads((ROOT/'Data/EthanV2/Native/bake_report.json').read_text())
originalNames=[m['baked'] for m in manifest['meshes']]
with bpy.data.libraries.load(str(ROOT/'Data/EthanV2/Native/Ethan_V2_73_Baked.blend'),link=False) as (src,dst):dst.objects=list(originalNames)
unchangedMaximum=0;comparisons=0
for name,original in zip(originalNames,dst.objects):
 target=bpy.data.objects[name]
 for oldKey in original.data.shape_keys.key_blocks:
  newKey=target.data.shape_keys.key_blocks[oldKey.name]
  a=np.empty(len(oldKey.data)*3,np.float32);oldKey.data.foreach_get('co',a)
  b=np.empty(len(newKey.data)*3,np.float32);newKey.data.foreach_get('co',b)
  delta=a.reshape(-1,3)-b.reshape(-1,3)[:len(oldKey.data)]
  error=float(np.linalg.norm(delta@np.array(target.matrix_world.to_3x3()).T,axis=1).max())
  assert error<3e-6,(name,oldKey.name,error);unchangedMaximum=max(unchangedMaximum,error);comparisons+=1
result={'passed':True,'brow_vertices':report['brow_vertices'],'head_vertices':len(head.data.vertices),'shape_comparisons_including_basis':74,'max_fbx_brow_position_error_m':maximum,'original_mesh_shape_comparisons':comparisons,'max_original_geometry_roundtrip_error_m':unchangedMaximum,'mesh_count':len(meshes),'hidden_authoring_exported':False,'materials':[m.name for m in head.data.materials]}
(OUT/'fbx_verification.json').write_text(json.dumps(result,indent=2));print('BROW_FBX_VERIFIED',json.dumps(result),flush=True)
