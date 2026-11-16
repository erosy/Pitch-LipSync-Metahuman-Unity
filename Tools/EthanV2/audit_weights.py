import bpy, json, pathlib, numpy as np
out={}
for o in bpy.data.objects:
 if o.type!='MESH' or not o.name.startswith('Ethan_'):continue
 totals={g.name:0 for g in o.vertex_groups}
 for v in o.data.vertices:
  for g in v.groups:totals[o.vertex_groups[g.group].name]+=g.weight
 out[o.name]={n:round(v,4) for n,v in totals.items() if v>.001}
pathlib.Path('D:/UnityProject/MetahumanTemplate/Data/EthanV2/weights_audit.json').write_text(json.dumps(out,indent=2))
print('WEIGHTS_SAVED')
