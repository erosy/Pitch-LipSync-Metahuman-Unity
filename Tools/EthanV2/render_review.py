import bpy, pathlib, sys, json, numpy as np
from mathutils import Vector
root=pathlib.Path('D:/UnityProject/MetahumanTemplate')
out=root/('Data/EthanV2/Eyelashes/Previews' if 'Eyelashes' in bpy.data.filepath or 'BrowsAndLashes' in bpy.data.filepath else 'Data/EthanV2/Eyebrows/Previews' if 'Eyebrows' in bpy.data.filepath else 'Data/EthanV2/Native/Previews' if 'Native' in bpy.data.filepath else 'Data/EthanV2/Previews');out.mkdir(parents=True,exist_ok=True)
for o in bpy.context.scene.objects:
 if o.type not in ('MESH','CURVES') or o.name.startswith(('CTRL','WGT','FRM','TEXT')):
  o.hide_render=True
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO';scene.display.shading.studiolight_rotate_z=0.4
scene.display.shading.color_type='MATERIAL';scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True;scene.display.shading.cavity_type='BOTH'
scene.display.shading.background_type='WORLD';scene.world.color=(0.12,0.12,0.12)
scene.render.resolution_x=640;scene.render.resolution_y=720;scene.render.resolution_percentage=100
data=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',data);scene.collection.objects.link(cam)
cam.location=(0,-0.9,1.78);target=Vector((0,-.025,1.73));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=.41;scene.camera=cam
shapes=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['Neutral']
for name in shapes:
 for o in bpy.data.objects:
  if o.type=='MESH' and o.data.shape_keys:
   for k in o.data.shape_keys.key_blocks[1:]:k.value=float(k.name==name)
 bpy.context.view_layer.update()
 scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
