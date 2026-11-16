"""Reconnect original DNA in a workspace copy. Never save over delivery files."""
import bpy, importlib, pathlib, json, numpy as np
ROOT=pathlib.Path('D:/UnityProject/MetahumanTemplate')
OUT=ROOT/'Data/EthanV2/Native';OUT.mkdir(parents=True,exist_ok=True)
DNA=pathlib.Path('C:/Users/ina17/Downloads/Ethan_Ver2_NewMetaHumanCharacter/Ethan_NewMetaHumanCharacter')
addon=importlib.import_module('bl_ext.api_portal_polyhammer_com_001.character_dna')
utilities=importlib.import_module(addon.__name__+'.utilities')
migration=importlib.import_module(addon.__name__+'.utilities.migration')
engine=importlib.import_module(addon.__name__+'.runtime.engine')
controller=importlib.import_module(addon.__name__+'.runtime.controller')
properties=utilities.get_addon_scene_properties(bpy.context)
result={'instances':[],'capability':engine.capability()}
for instance in properties.rig_instance_list:
    instance.head_dna_file_path=str(DNA/'head.dna')
    instance.body_dna_file_path=str(DNA/'body.dna')
    result['instances'].append({'name':instance.name,'head_rig':getattr(instance.head_rig,'name',None),'body_rig':getattr(instance.body_rig,'name',None),'control_rig':getattr(instance.control_rig,'name',None),'head_mesh':getattr(instance.head_mesh,'name',None),'head_coordinate_version':instance.head_rig.get('dna_coordinate_version',0) if instance.head_rig else None})
for obj in bpy.data.objects:
    if obj.get('character_dna_native_carrier'):
        obj['head_dna_file_path']=str(DNA/'head.dna');obj['body_dna_file_path']=str(DNA/'body.dna')
print('BEFORE_MIGRATION',json.dumps(result),flush=True)
try:
    result['migration']=migration.migrate_runtime_data(bpy.context,migrate_actions=True,rebuild_controls=False)
    for instance in properties.rig_instance_list:
        controller.rebuild(instance)
        result['instances_after']=[]
        result['instances_after'].append({'name':instance.name,'binding_issues':engine.binding_issues(instance),'head_initialized':instance.head_initialized,'errors':engine._errors})
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ethan_V2_Reconnected.blend'))
    result['saved']=str(OUT/'Ethan_V2_Reconnected.blend')
except Exception as exc:
    import traceback
    result['error']=str(exc);result['traceback']=traceback.format_exc()
    print(result['traceback'],flush=True)
(OUT/'reconnection_report.json').write_text(json.dumps(result,indent=2,default=str))
print('RECONNECTION_RESULT',json.dumps(result,default=str),flush=True)
