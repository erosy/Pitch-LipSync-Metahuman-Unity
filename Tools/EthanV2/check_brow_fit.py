import bpy,numpy as np,pathlib,json
from mathutils.bvhtree import BVHTree
from mathutils import Vector
root=pathlib.Path('D:/UnityProject/MetahumanTemplate');out=root/'Data/EthanV2/Eyebrows'
head=bpy.data.objects['BAKED_head_lod0_mesh'];groom=bpy.data.objects['BAKED_Groom_0'];report=json.loads((out/'export_report.json').read_text());start=report['head_vertices_before']
def coords(data):
 a=np.empty(len(data)*3,np.float32);data.foreach_get('co',a);return a.reshape(-1,3).astype(float)
bpy.context.view_layer.update();ev=head.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ev.to_mesh();vertices=coords(m.vertices);ev.to_mesh_clear()
mw=np.array(head.matrix_world);vertices=vertices@mw[:3,:3].T+mw[:3,3]
brow=vertices[start:].reshape(-1,32,3,3).mean(axis=2)
a=np.empty(len(groom.data.points)*3,np.float32);groom.data.attributes['position'].data.foreach_get('vector',a);a=a.reshape(-1,3);gm=np.array(groom.matrix_world);a=a@gm[:3,:3].T+gm[:3,3]
roots=[];tips=[];maxshape=0
for j,c in enumerate(groom.data.curves):
 p=a[c.first_point_index:c.first_point_index+c.points_length];roots.append(np.linalg.norm(brow[j,0]-p[0]));tips.append(np.linalg.norm(brow[j,-1]-p[-1]))
radius=groom.data.attributes.get('radius');radii=None
if radius:
 v=np.empty(len(radius.data),np.float32);radius.data.foreach_get('value',v);scale=np.linalg.norm(gm[:3,0]);radii={'min_m':float(v.min()*scale),'median_m':float(np.median(v)*scale),'max_m':float(v.max()*scale)}
result={'strands':len(brow),'max_evaluated_root_error_m':max(roots),'max_evaluated_tip_error_m':max(tips),'embedded_world_bounds':[brow.reshape(-1,3).min(0).tolist(),brow.reshape(-1,3).max(0).tolist()],'source_world_bounds':[a.min(0).tolist(),a.max(0).tolist()],'source_radius_attribute':radii,'embedded_radius_m':report['diameter_m']/2,'source_materials':[m.name for m in groom.data.materials if m]}
(out/'fit_check.json').write_text(json.dumps(result,indent=2));print('FIT_CHECK',json.dumps(result),flush=True)
