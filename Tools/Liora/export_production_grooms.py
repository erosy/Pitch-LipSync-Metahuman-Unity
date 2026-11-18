"""Sample delivered working groom bindings and build production geometry.
Writes only new workspace outputs. Does not modify the baked/source blend.
"""
import bpy,numpy as np,pathlib,json,struct,itertools,hashlib
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree
from mathutils import Vector
ROOT=pathlib.Path('D:/UnityProject/MetahumanTemplate');OUT=ROOT/'Data/Liora/Integration'
DEST=ROOT/'Assets/_Sandbox/Characters/Liora/Groom';DEST.mkdir(exist_ok=True)
head=bpy.data.objects['BAKED_head_lod0_mesh'];names=[k.name for k in head.data.shape_keys.key_blocks]
assert len(names)==74
def world(o,curves=False):
 e=o.evaluated_get(bpy.context.evaluated_depsgraph_get());d=e.data
 if curves:
  p=np.empty(len(d.points)*3,np.float32);d.attributes['position'].data.foreach_get('vector',p)
 else:
  p=np.empty(len(d.vertices)*3,np.float32);d.vertices.foreach_get('co',p)
 m=np.array(e.matrix_world);return p.reshape(-1,3)@m[:3,:3].T+m[:3,3]
neutral=world(head)
kd=KDTree(len(neutral))
for j,p in enumerate(neutral):kd.insert(p,j)
kd.balance()
raw=(OUT/'unity_head_world.bin').read_bytes();n=struct.unpack('<i',raw[:4])[0];unityhead=np.frombuffer(raw[4:],'<f4').reshape(n,3)
sample=unityhead[np.linspace(0,n-1,600,dtype=int)];candidates=[]
for perm in itertools.permutations(range(3)):
 for signs in itertools.product([-1,1],repeat=3):
  q=np.eye(3)[list(perm)]*np.array(signs)[:,None]
  err=max(kd.find(p)[2] for p in sample@q)
  candidates.append((err,q))
match,Q=min(candidates,key=lambda x:x[0]);assert match<3e-6,match
audit=json.loads((OUT/'inspection.json').read_text());hm=np.array(next(b for b in audit['target']['bones'] if not b['path'].startswith('BAKED_LioraMinji_NewMetaHumanCharacter_head_rig'))['matrix']).reshape(4,4)
hm[:3,:3]/=np.linalg.norm(hm[:3,:3],axis=0);inv=np.linalg.inv(hm)
def convert(p):return (p@Q.T)@inv[:3,:3].T+inv[:3,3]
grooms=sorted([o for o in bpy.data.objects if o.type=='CURVES' and o.name.startswith('BAKED_Groom_')],key=lambda o:len(o.data.curves))
lashes,brows,scalp=grooms;assert len(lashes.data.curves)==816 and len(brows.data.curves)==1593
samples={o.name:[] for o in [brows,lashes]}
for name in names:
 for k in head.data.shape_keys.key_blocks:k.value=float(k.name==name and name!='Basis')
 bpy.context.view_layer.update()
 for o in [brows,lashes]:samples[o.name].append(convert(world(o,True)))
 print('SAMPLED_GROOM',name,flush=True)
for k in head.data.shape_keys.key_blocks:k.value=0
bpy.context.view_layer.update()
reports=[]
for label,o,radius in [('Eyebrows',brows,.000125),('Eyelashes',lashes,.00006)]:
 poses=np.array(samples[o.name]);vertices=[[] for _ in names];normals=[[] for _ in names];triangles=[];uv=[];ring_counts=[];errors=[];start=0
 for curve in o.data.curves:
  p=poses[:,curve.first_point_index:curve.first_point_index+curve.points_length];count=p.shape[1]
  selected=sorted(set([0,count//2,count-1]))
  def error():
   values=[]
   for lo,hi in zip(selected[:-1],selected[1:]):
    if hi-lo<=1:continue
    a=p[:,lo:lo+1];d=p[:,hi:hi+1]-a;f=np.clip(np.sum((p[:,lo:hi+1]-a)*d,axis=2)/np.maximum(np.sum(d*d,axis=2),1e-24),0,1)
    err=np.linalg.norm(p[:,lo:hi+1]-(a+f[:,:,None]*d),axis=2);idx=np.unravel_index(err.argmax(),err.shape)
    values.append((float(err[idx]),lo+idx[1]))
   return max(values,default=(0,None))
  while len(selected)<min(12,count):
   e,idx=error()
   if e<=.000075:break
   selected=sorted(selected+[idx])
  errors.append(error()[0]);ring_counts.append(len(selected));path=p[:,selected]
  lengths=np.r_[0,np.cumsum(np.linalg.norm(np.diff(p[0],axis=0),axis=1))];fractions=lengths[selected]/max(lengths[-1],1e-15)
  # Authoring radius is the Alembic placeholder 0.5, not a physical width.
  # Use established visible strand widths and a smooth root-to-tip taper.
  radii=radius*(1-.85*fractions**2)
  for pose in range(len(names)):
   centers=path[pose];tangent=np.gradient(centers,axis=0);tangent/=np.maximum(np.linalg.norm(tangent,axis=1)[:,None],1e-12)
   reference=np.array([0,0,1.])
   if np.max(np.abs(tangent@reference))>.95:reference=np.array([0,1.,0])
   ax=np.cross(tangent,reference);ax/=np.maximum(np.linalg.norm(ax,axis=1)[:,None],1e-12);ay=np.cross(tangent,ax)
   radial=np.stack([np.cos(a)*ax+np.sin(a)*ay for a in [0,2*np.pi/3,4*np.pi/3]],axis=1)
   vertices[pose].append((centers[:,None,:]+radii[:,None,None]*radial).reshape(-1,3));normals[pose].append(radial.reshape(-1,3))
  for j,f in enumerate(fractions):uv.extend([[side/3,float(f)] for side in range(3)])
  for j in range(len(selected)-1):
   for side in range(3):
    a=start+j*3+side;b=start+j*3+(side+1)%3;c=b+3;d=a+3
    triangles.extend([[a,c,b],[a,d,c]])
  start+=len(selected)*3
 v=np.array([np.concatenate(p) for p in vertices],np.float32);norm=np.array([np.concatenate(p) for p in normals],np.float32);t=np.array(triangles,np.int32)
 # Correct winding to agree with the authored radial shading normal.
 cross=np.cross(v[0,t[:,1]]-v[0,t[:,0]],v[0,t[:,2]]-v[0,t[:,0]])
 if np.sum(cross*norm[0,t[:,0]])<0:t=t[:,[0,2,1]]
 with (OUT/(label+'.bin')).open('wb') as f:
  f.write(struct.pack('<iii',len(v[0]),len(t)*3,len(names)-1));f.write(v[0].tobytes());f.write(norm[0].tobytes());f.write(np.array(uv,np.float32).tobytes());f.write(t.tobytes())
  for j in range(1,len(names)):f.write((v[j]-v[0]).tobytes());f.write((norm[j]-norm[0]).tobytes())
 report={'name':label,'source':o.name,'strands':len(o.data.curves),'vertices':len(v[0]),'triangles':len(t),'rings_min':min(ring_counts),'rings_max':max(ring_counts),'rings_mean':float(np.mean(ring_counts)),'max_curve_error_m':max(errors),'root_tip_center_error_m':0,'root_radius_m':radius,'tip_radius_m':radius*.15,'source_radius_note':'Uniform 0.5 Alembic placeholder; use production physical widths with taper. Source control points and source file preserved.','position_blendshape_bytes':int((v[1:].size)*4),'position_and_normal_blendshape_bytes':int(v[1:].size*8),'expressions':names[1:]}
 reports.append(report);print('FACIAL_GROOM',json.dumps(report),flush=True)
# Scalp: keep actual delivered strand order and all authored roots and tips.
p=convert(world(scalp,True));curves=[];endpoint_error=0;degenerate=0
for c in scalp.data.curves:
 path=p[c.first_point_index:c.first_point_index+c.points_length];s=np.r_[0,np.cumsum(np.linalg.norm(np.diff(path,axis=0),axis=1))]
 if s[-1]<1e-12:res=np.repeat(path[:1],32,axis=0);degenerate+=1
 else:res=np.stack([np.interp(np.linspace(0,s[-1],32),s,path[:,j]) for j in range(3)],axis=1)
 res[0]=path[0];res[-1]=path[-1];curves.append(res)
curves=np.array(curves,np.float32)
with (DEST/'Liora_Scalp32.bytes').open('wb') as f:f.write(struct.pack('<iii',0x48414952,len(curves),32));f.write(curves.tobytes())
# Collision SDF in the same measured head frame, x varying fastest.
v=convert(neutral);head.data.calc_loop_triangles();tri=np.array([t.vertices[:] for t in head.data.loop_triangles])
if np.linalg.det(Q@np.array(head.matrix_world)[:3,:3])<0:tri=tri[:,::-1]
bvh=BVHTree.FromPolygons(v.tolist(),tri.tolist(),all_triangles=True);lo=v.min(0)-.025;hi=v.max(0)+.025;n=96;volume=np.empty((n,n,n),np.float32)
for z in range(n):
 for y in range(n):
  for x in range(n):
   point=lo+(hi-lo)*(np.array([x,y,z])+.5)/n;hit,normal,index,d=bvh.find_nearest(Vector(point));volume[z,y,x]=d if np.dot(point-np.array(hit),np.array(normal))>=0 else -d
 if z%24==0:print('SDF_SLICE',z,flush=True)
(DEST/'Liora_HeadSDF.bytes').write_bytes(volume.tobytes())
report={'source_blend':bpy.data.filepath,'source_sha256':hashlib.sha256(pathlib.Path(bpy.data.filepath).read_bytes()).hexdigest(),'coordinate_matrix':Q.tolist(),'max_imported_head_correspondence_m':match,'normalized_head_matrix':hm.tolist(),'scalp_object':scalp.name,'strands':len(curves),'points_per_strand':32,'degenerate_strands':degenerate,'root_tip_float32_error_m':float(max(np.linalg.norm(curves[:,0].astype(float)-p[[c.first_point_index for c in scalp.data.curves]],axis=1).max(),np.linalg.norm(curves[:,-1].astype(float)-p[[c.first_point_index+c.points_length-1 for c in scalp.data.curves]],axis=1).max())),'sdf_resolution':n,'sdf_min':lo.tolist(),'sdf_max':hi.tolist(),'sdf_range':[float(volume.min()),float(volume.max())],'facial_hair':reports,'shape_names':names[1:]}
(OUT/'groom_export.json').write_text(json.dumps(report,indent=2));print('PRODUCTION_GROOM_EXPORT_COMPLETE',flush=True)
