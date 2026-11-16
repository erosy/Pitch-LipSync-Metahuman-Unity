import bpy,pathlib,numpy as np
p=pathlib.Path('C:/Users/ina17/Downloads/Ethan_Ver2_NewMetaHumanCharacter/Ethan_NewMetaHumanCharacter/groom')
for file in ['SC_Hairstyle_Male_11.ABC','Hair_Overall.abc']:
 before=set(bpy.data.objects);bpy.ops.wm.alembic_import(filepath=str(p/file),as_background_job=False)
 for o in set(bpy.data.objects)-before:
  if o.type=='CURVES':print('ABC',file,o.name,len(o.data.curves),len(o.data.points),flush=True)
  if o.type=='CURVE':print('ABC',file,o.name,len(o.data.splines),sum(len(s.points)+len(s.bezier_points) for s in o.data.splines),flush=True)
