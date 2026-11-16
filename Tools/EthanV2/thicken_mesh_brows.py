"""Revise strand cross sections without moving any strand centerline."""
import bpy,numpy as np,json,pathlib
ROOT=pathlib.Path('D:/UnityProject/MetahumanTemplate');OUT=ROOT/'Data/EthanV2/Eyebrows'
head=bpy.data.objects['BAKED_head_lod0_mesh'];report=json.loads((OUT/'export_report.json').read_text());start=report['head_vertices_before'];count=report['strands']
t=np.linspace(0,1,32);old=.000045*np.maximum(.08,1-t**3)
# Match the source's full profile, with only the last two samples tapering.
new=np.full(32,.000125);new[-2]=.00009;new[-1]=.000025
factor=(new/old)[None,:,None,None]
expected={};maxCenter=0;updatedBasis=None
for key in head.data.shape_keys.key_blocks:
 a=np.empty(len(key.data)*3,np.float32);key.data.foreach_get('co',a);a=a.reshape(-1,3)
 tube=a[start:].astype(np.float64).reshape(count,32,3,3);center=tube.mean(axis=2,keepdims=True)
 tube=center+(tube-center)*factor
 maxCenter=max(maxCenter,float(np.max(np.abs(tube.mean(axis=2)-center[:,:,0,:]))))
 a[start:]=tube.reshape(-1,3);key.data.foreach_set('co',a.ravel());expected[key.name]=a[start:].copy()
 if key==head.data.shape_keys.reference_key:updatedBasis=a.copy()
head.data.vertices.foreach_set('co',updatedBasis.ravel())
head.data.update()
report.update(diameter_m=.00025,tip_diameter_m=.00005,profile='Full width through 30/32 samples, then tapered tip; source constant-radius visual match',previous_diameter_m=.00009,max_centerline_change_m=maxCenter)
(OUT/'export_report.json').write_text(json.dumps(report,indent=2));np.savez_compressed(OUT/'brow_baselines.npz',**expected)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ethan_V2_73_WithEyebrows.blend'))
for o in bpy.context.selected_objects:o.select_set(False)
for o in head.users_collection[0].objects:
 if o.type in ['MESH','ARMATURE']:o.hide_set(False);o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Ethan_V2_73_WithEyebrows.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
print('THICKNESS_UPDATED',json.dumps({'diameter_mm':.25,'max_centerline_change_m':maxCenter}),flush=True)
