import bpy
bpy.ops.import_scene.fbx(filepath='D:/UnityProject/MetahumanTemplate/Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2_73_Baked.fbx')
for o in bpy.context.scene.objects:
 if o.type=='MESH':print('MESH',o.name,len(o.data.vertices),len(o.data.shape_keys.key_blocks) if o.data.shape_keys else 0,[(m.type,getattr(getattr(m,'object',None),'name',None)) for m in o.modifiers])
 if o.type=='ARMATURE':print('ARMATURE',o.name,len(o.data.bones))
