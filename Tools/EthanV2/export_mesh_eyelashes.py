"""Convert Ethan's lash groom to head-integrated mesh strands; preserve approved brows."""
import bpy,numpy as np,pathlib,json,hashlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=pathlib.Path('D:/UnityProject/MetahumanTemplate');OUT=ROOT/'Data/EthanV2/Eyelashes';OUT.mkdir(parents=True,exist_ok=True)
head=bpy.data.objects['BAKED_head_lod0_mesh'];groom=bpy.data.objects['BAKED_Groom_Group_0']
names=[k.name for k in head.data.shape_keys.key_blocks]
assert len(names)==74
def coords(data):
 a=np.empty(len(data)*3,np.float32);data.foreach_get('co',a);return a.reshape(-1,3).astype(np.float64)
base=coords(head.data.shape_keys.key_blocks[0].data)
head.data.calc_loop_triangles();tri=np.array([t.vertices[:] for t in head.data.loop_triangles if max(t.vertices)<19250],np.int32)
bvh=BVHTree.FromPolygons(base.tolist(),tri.tolist(),all_triangles=True)
raw=np.empty(len(groom.data.points)*3,np.float32);groom.data.attributes['position'].data.foreach_get('vector',raw);raw=raw.reshape(-1,3)
transform=np.array(head.matrix_world.inverted()@groom.matrix_world)
raw=raw@transform[:3,:3].T+transform[:3,3]
curves=[]
for curve in groom.data.curves:
 points=raw[curve.first_point_index:curve.first_point_index+curve.points_length]
 length=np.r_[0,np.cumsum(np.linalg.norm(np.diff(points,axis=0),axis=1))]
 assert length[-1]>0
 # 32 samples, preserving authored root/tip. Geometry has no simulation.
 sample=np.linspace(0,length[-1],32)
 curves.append(np.stack([np.interp(sample,length,points[:,i]) for i in range(3)],axis=1))
curves=np.array(curves);count=len(curves)
def frame(v):
 x=v[1]-v[0];x/=np.linalg.norm(x);z=np.cross(x,v[2]-v[0]);z/=np.linalg.norm(z);return np.stack([x,np.cross(z,x),z],axis=1)
bindings=[];local=[];surface_dist=[];weights=[]
for strand in curves:
 point,normal,index,dist=bvh.find_nearest(Vector(strand[0]));v=base[tri[index]];f=frame(v)
 uv=np.linalg.lstsq(np.stack([v[1]-v[0],v[2]-v[0]],axis=1),np.array(point)-v[0],rcond=None)[0]
 bary=np.array([1-uv.sum(),uv[0],uv[1]])
 bindings.append((index,bary,f));surface_dist.append(dist)
 # Triangular cross-section, 0.14 mm root diameter, tapering over the final 40% of the lash.
 tangent=np.gradient(strand,axis=0);tangent/=np.linalg.norm(tangent,axis=1)[:,None]
 axis=np.cross(tangent,f[:,2]);axis/=np.maximum(np.linalg.norm(axis,axis=1)[:,None],1e-12)
 second=np.cross(tangent,axis)
 t=np.linspace(0,1,32);radius=.00007*(1-.9*np.clip((t-.6)/.4,0,1))
 tube=np.array([strand+radius[:,None]*(np.cos(a)*axis+np.sin(a)*second) for a in [0,2*np.pi/3,4*np.pi/3]]).transpose(1,0,2).reshape(-1,3)
 local.append((tube-np.array(point))@f)
 w={}
 for vertex,b in zip(tri[index],bary):
  for group in head.data.vertices[int(vertex)].groups:
   n=head.vertex_groups[group.group].name;w[n]=w.get(n,0)+float(b)*group.weight
 weights.append(w)
local=np.array(local)
def deform(target):
 result=[]
 for j,(index,bary,old) in enumerate(bindings):
  v=target[tri[index]];f=frame(v);origin=bary@v;result.append(origin+local[j]@f.T)
 return np.array(result).reshape(-1,3)
neutral=deform(base)
faces=[]
for j in range(count):
 start=j*96
 for k in range(31):
  for side in range(3):
   a=start+k*3+side;b=start+k*3+(side+1)%3;faces.append((a,b,b+3,a+3))
mesh=bpy.data.meshes.new('Ethan_EyelashStrands_Mesh');mesh.from_pydata(neutral.tolist(),[],faces);mesh.update()
obj=bpy.data.objects.new('Ethan_EyelashStrands',mesh);head.users_collection[0].objects.link(obj);obj.matrix_world=head.matrix_world.copy()
for p in mesh.polygons:p.use_smooth=True
for j,w in enumerate(weights):
 indices=list(range(j*96,(j+1)*96))
 for n,value in w.items():
  if value>1e-6:
   group=obj.vertex_groups.get(n) or obj.vertex_groups.new(name=n);group.add(indices,value,'REPLACE')
material=bpy.data.materials.new('Ethan_V2_EyelashStrands');material.diffuse_color=(.006,.003,.002,1);material.use_nodes=True
bsdf=material.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=material.diffuse_color;bsdf.inputs['Roughness'].default_value=.65;obj.data.materials.append(material)
expected={};movement={}
for key in head.data.shape_keys.key_blocks:
 target=deform(coords(key.data));shape=obj.shape_key_add(name=key.name);shape.value=0;shape.data.foreach_set('co',target.astype(np.float32).ravel());expected[key.name]=target.astype(np.float32)
 movement[key.name]=float(np.linalg.norm(target-neutral,axis=1).max())
print('LASHES_BAKED',count,len(neutral),max(surface_dist),flush=True)
# Join as a material section of the head so one set of sliders drives the brows.
before=len(head.data.vertices)
for o in bpy.context.selected_objects:o.select_set(False)
obj.select_set(True);head.hide_set(False);head.select_set(True);bpy.context.view_layer.objects.active=head
bpy.ops.object.join();assert len(head.data.vertices)==before+len(neutral)
for key in head.data.shape_keys.key_blocks:
 actual=coords(key.data)[before:];assert np.max(np.abs(actual-expected[key.name]))<1e-6
groom.hide_render=True;groom.hide_set(True)
cards=bpy.data.objects['BAKED_eyelashes_lod0_mesh'];cards.hide_render=True;cards.hide_set(True)
report={'source_groom':groom.name,'strands':count,'samples_per_strand':32,'sides':3,'lash_vertices':len(neutral),'head_vertices_before':before,'head_vertices_after':len(head.data.vertices),'shape_names':names[1:],'max_root_surface_distance_m':max(surface_dist),'movement_m':movement,'diameter_m':.00014,'tip_diameter_m':.000014,'scalp_changed':False,'brows_changed':False,'replaces_original_lash_cards':True,'material':material.name}
(OUT/'export_report.json').write_text(json.dumps(report,indent=2));np.savez_compressed(OUT/'lash_baselines.npz',**expected)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Ethan_V2_73_WithBrowsAndLashes.blend'))
for o in bpy.context.selected_objects:o.select_set(False)
export=[o for o in head.users_collection[0].objects if o.type in ['MESH','ARMATURE'] and o!=cards]
for o in export:o.hide_set(False);o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'Ethan_V2_73_WithBrowsAndLashes.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
print('BROW_EXPORT_SAVED',flush=True)
