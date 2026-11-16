import bpy,json
for m in bpy.data.materials:
 if any(x in m.name.lower() for x in ['eyeedge','eyeshell','saliva']):
  print('MATERIAL',m.name,'diffuse',list(m.diffuse_color))
  if m.node_tree:
   for n in m.node_tree.nodes:
    print(n.type,n.name, getattr(getattr(n,'image',None),'name',None),[(i.name,str(i.default_value)) for i in n.inputs if not i.is_linked and hasattr(i,'default_value') and i.name in ['Base Color','Color','Alpha','Roughness','IOR','Transmission Weight']])
