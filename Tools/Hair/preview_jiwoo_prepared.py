"""Render a neutral inspection preview without saving changes to the blend."""
from pathlib import Path

import bpy
from mathutils import Vector


scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x = 800
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'OBJECT'
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.background_type = 'WORLD'
scene.world.color = (0.08, 0.08, 0.08)
bpy.data.objects['Face'].color = (0.58, 0.40, 0.32, 1)
bpy.data.objects['Hair'].color = (0.025, 0.019, 0.015, 1)
bpy.data.objects['Jiwoo_Armature'].hide_render = True
camera_data = bpy.data.cameras.new('Batch02_PreviewCamera')
camera = bpy.data.objects.new('Batch02_PreviewCamera', camera_data)
scene.collection.objects.link(camera)
camera.location = (0.7, -2.8, 1.9)
target = Vector((0, 0, 1.45))
camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera_data.type = 'ORTHO'
camera_data.ortho_scale = 0.88
scene.camera = camera
output = Path(__file__).resolve().parents[2] / 'Data/JiwooReimport/Batch02/prepared_preview.png'
scene.render.filepath = str(output)
bpy.ops.render.render(write_still=True)
print('JIWOO_PREVIEW ' + str(output), flush=True)
