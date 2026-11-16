import bpy,numpy as np,json,pathlib
o=bpy.data.objects['BAKED_Groom_0'];ev=o.evaluated_get(bpy.context.evaluated_depsgraph_get());d=ev.data
result={'modifiers':[],'attributes':[]}
for a in d.attributes:
 if a.name in ['radius','position']:
  row={'name':a.name,'type':a.data_type,'count':len(a.data)}
  if a.name=='radius':
   v=np.empty(len(a.data),np.float32);a.data.foreach_get('value',v);row.update(min=float(v.min()),median=float(np.median(v)),max=float(v.max()),worldScale=float(o.matrix_world.to_scale().length/(3**.5)))
  result['attributes'].append(row)
for m in o.modifiers:
 row={'name':m.name,'type':m.type}
 if m.type=='NODES' and m.node_group:
  row['nodes']=[{'name':n.name,'type':n.type,'group':getattr(getattr(n,'node_tree',None),'name',None),'inputs':{i.name:str(i.default_value) for i in n.inputs if hasattr(i,'default_value') and not i.is_linked}} for n in m.node_group.nodes]
 result['modifiers'].append(row)
for mat in o.data.materials:
 if mat and mat.node_tree:result['material_nodes']=[{'name':n.name,'type':n.type,'inputs':{i.name:str(i.default_value) for i in n.inputs if hasattr(i,'default_value') and not i.is_linked}} for n in mat.node_tree.nodes]
p=pathlib.Path('D:/UnityProject/MetahumanTemplate/Data/EthanV2/Eyebrows/radius_inspection.json');p.write_text(json.dumps(result,indent=2));print(json.dumps(result),flush=True)
