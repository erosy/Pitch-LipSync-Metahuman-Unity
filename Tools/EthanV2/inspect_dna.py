import bpy,json,pathlib
out=pathlib.Path('D:/UnityProject/MetahumanTemplate/Data/EthanV2')
result={'objects':{},'texts':{},'packed_files':[]}
def props(id):
 d={}
 for k in id.keys():
  v=id[k]
  if hasattr(v,'to_dict'):v=v.to_dict()
  elif hasattr(v,'to_list'):v=v.to_list()
  d[k]=v
 return d
for o in bpy.data.objects:
 if 'native' in o.name or o.name.endswith(('_head_rig','_face_gui')):
  result['objects'][o.name]=props(o)
  if o.type=='ARMATURE':result['objects'][o.name]['data_props']=props(o.data)
for t in bpy.data.texts:
 result['texts'][t.name]={'length':len(t.as_string()),'filepath':t.filepath}
 (out/t.name).write_text(t.as_string())
result['scene_props']=props(bpy.context.scene)
result['fileprops']=props(bpy.data.collections.get('Collection')) if bpy.data.collections.get('Collection') else {}
(out/'dna_dependencies.json').write_text(json.dumps(result,indent=2,default=str))
print('DNA_DEPENDENCIES_SAVED')
