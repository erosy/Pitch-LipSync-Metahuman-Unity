import bpy, importlib, pathlib, json, numpy as np
A='bl_ext.api_portal_polyhammer_com_001.character_dna';u=importlib.import_module(A+'.utilities');e=importlib.import_module(A+'.runtime.engine');c=importlib.import_module(A+'.runtime.controller')
i=u.get_addon_scene_properties().rig_instance_list[0];i.initialize();c.rebuild(i)
result={'flags':{n:getattr(i,n) for n in ['auto_evaluate','auto_evaluate_head','evaluate_bones','evaluate_shape_keys','evaluate_texture_masks']},'cached_blocks':sum(len(v) for v in i.head_shape_key_blocks.values()),'head_shape_driver_count':len(i.head_mesh.data.shape_keys.animation_data.drivers) if i.head_mesh.data.shape_keys.animation_data else 0,'carrier_shapes':[],'tests':[]}
def coords():
 ev=i.head_mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ev.to_mesh();a=np.empty(len(m.vertices)*3,np.float32);m.vertices.foreach_get('co',a);ev.to_mesh_clear();return a.reshape(-1,3)
neutral=None
for carrier in e.carriers(i):
 result['carrier_shapes'].append({'name':carrier.name,'targets':len(carrier.get('targets',())),'settings':str(carrier.get('settings'))})
for value in [0,1]:
 i.face_board.pose.bones['CTRL_L_eye_blink'].location.y=value;i.face_board.update_tag();bpy.context.view_layer.update();bpy.context.scene.frame_set(bpy.context.scene.frame_current+1);bpy.context.view_layer.update()
 shapes=[(k.name,k.value) for k in i.head_mesh.data.shape_keys.key_blocks if abs(k.value)>1e-6]
 before=coords()
 if neutral is None:neutral=before.copy()
 i.head_mesh.data.shape_keys.update_tag();i.head_mesh.data.update();bpy.context.view_layer.update()
 after=coords()
 result['tests'].append({'blink':value,'active_shapes':shapes,'drivers_valid':all(f.driver.is_valid for f in i.head_mesh.data.shape_keys.animation_data.drivers) if i.head_mesh.data.shape_keys.animation_data else False,'max_before_tag_m':float(np.linalg.norm(before-neutral,axis=1).max()),'max_after_tag_m':float(np.linalg.norm(after-neutral,axis=1).max())})
print('LIVE_CORRECTIVES',json.dumps(result,default=str),flush=True)
pathlib.Path('D:/UnityProject/MetahumanTemplate/Data/EthanV2/Native/live_corrective_test.json').write_text(json.dumps(result,indent=2,default=str))
