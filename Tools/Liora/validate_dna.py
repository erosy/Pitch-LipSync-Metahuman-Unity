import bpy, importlib, pathlib, json, hashlib
root=pathlib.Path('D:/UnityProject/MetahumanTemplate');out=root/'Data/Liora/Native';out.mkdir(parents=True,exist_ok=True)
dna_root=pathlib.Path('C:/Users/ina17/Downloads/LioraMinji_DNA_NewMetaHumanCharacter/LioraMinji_NewMetaHumanCharacter')
addon='bl_ext.api_portal_polyhammer_com_001.character_dna'
get_reader=importlib.import_module(addon+'.dna_io').get_dna_reader
engine=importlib.import_module(addon+'.runtime.engine')
result={'runtime':engine.capability(),'components':[]}
for name in ['head','body']:
 p=dna_root/(name+'.dna');r=get_reader(file_path=p,file_format='binary')
 result['components'].append({'component':name,'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'name':r.getName(),'joint_count':r.getJointCount(),'mesh_count':r.getMeshCount(),'lod0_meshes':[{'name':r.getMeshName(i),'vertices':r.getVertexPositionCount(i),'blendshape_targets':r.getBlendShapeTargetCount(i)} for i in r.getMeshIndicesForLOD(0)],'gui_controls':[r.getGUIControlName(i) for i in range(r.getGUIControlCount())],'raw_controls':[r.getRawControlName(i) for i in range(r.getRawControlCount())]})
(out/'dna_validation.json').write_text(json.dumps(result,indent=2))
print('DNA_VALIDATION',json.dumps({'runtime':result['runtime'],'components':[{k:v for k,v in c.items() if k not in ['gui_controls','raw_controls']} for c in result['components']]}),flush=True)

