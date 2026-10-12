import bpy, json, pathlib, struct, time
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

root=pathlib.Path(r'C:/PROJECTS/Metahuman Unity Lip Sync Workaround')
out=root/'Assets/_Sandbox/Groom Hair Asset/Fitted';out.mkdir(exist_ok=True)
j=json.loads((root/'Temp/ethan_head_local.json').read_text())
verts=np.asarray(j['vertices']);tris=np.asarray(j['triangles']).reshape(-1,3)
bvh=BVHTree.FromPolygons(verts.tolist(),tris.tolist(),all_triangles=True)
rig=bpy.data.objects['rig'];head=rig.pose.bones['head'];inv=np.linalg.inv(np.array(rig.matrix_world@head.matrix))
model=bpy.data.objects['Ethan_NewMetaHumanCharacter_head_lod0_mesh.003']
base=np.array([list(v.co) for v in model.data.vertices]);m=inv@np.array(model.matrix_world);reference=base@m[:3,:3].T+m[:3,3]
source=reference[(reference[:,1]>.055)&(reference[:,1]<.23)][::8]
# Register skull surfaces with a small rigid correction; preserve the fitted groom scale.
R=np.eye(3);t=np.zeros(3)
for iteration in range(12):
 p=source@R.T+t;hits=[];valid=[]
 for k,v in enumerate(p):
  hit,n,idx,d=bvh.find_nearest(Vector(v))
  if d<.025:valid.append(k);hits.append(hit)
 a=p[valid];b=np.asarray(hits);ac=a.mean(0);bc=b.mean(0);u,s,vt=np.linalg.svd((a-ac).T@(b-bc));step=vt.T@u.T
 if np.linalg.det(step)<0:vt[-1]*=-1;step=vt.T@u.T
 shift=bc-step@ac;R=step@R;t=step@t+shift
errors=[bvh.find_nearest(Vector(v))[3] for v in source@R.T+t]
hair=bpy.data.objects['01_Center part curtains.001'];data=hair.data
def attribute(name):
 a=np.empty(len(data.points)*3,dtype=np.float32);data.attributes[name].data.foreach_get('vector',a);return a.reshape(-1,3)
positions=attribute('position');left=attribute('handle_left');right=attribute('handle_right')
types=np.empty(len(data.curves),dtype=np.int32);data.attributes['curve_type'].data.foreach_get('value',types)
mat=inv@np.array(hair.matrix_world);mat[:3,:3]=R@mat[:3,:3];mat[:3,3]=R@mat[:3,3]+t
result=np.empty((len(data.curves),32,3),dtype=np.float32);corrected=0
for i,c in enumerate(data.curves):
 start=c.first_point_index;n=len(c.points);p=positions[start:start+n]
 if types[i]==2:
  u=np.linspace(0,1,12)[:,None];segments=[]
  for k in range(n-1):segments.append(((1-u)**3*p[k]+3*(1-u)**2*u*right[start+k]+3*(1-u)*u**2*left[start+k+1]+u**3*p[k+1])[:-1])
  dense=np.vstack(segments+[p[-1:]])
 else:dense=p
 lengths=np.r_[0,np.cumsum(np.linalg.norm(np.diff(dense,axis=0),axis=1))]
 targets=np.linspace(0,lengths[-1],32);points=np.column_stack([np.interp(targets,lengths,dense[:,a]) for a in range(3)])
 points=points@mat[:3,:3].T+mat[:3,3]
 # Roots are fixed by the solver: lift only roots that are behind the local surface.
 hit,normal,idx,d=bvh.find_nearest(Vector(points[0]));signed=(Vector(points[0])-hit).dot(normal)
 if signed<.0005 and d<.035:
  displacement=np.array(hit+normal*.0008)-points[0]
  points+=np.maximum(0,1-np.arange(32)/8)[:,None]*displacement;corrected+=1
 result[i]=points
 if i%25000==0:print('Exported curves',i,flush=True)
with (out/'Ethan_BlenderFitted.bytes').open('wb') as f:
 f.write(struct.pack('<iii',0x48414952,len(result),32));f.write(result.astype('<f4').tobytes())
# SDF covers skull, ears and the upper neck, with an exterior border for sampling.
low=np.array([-.15,-.075,-.14]);high=np.array([.15,.28,.19]);N=96;size=high-low;field=np.empty((N,N,N),dtype=np.float32)
for z in range(N):
 for y in range(N):
  for x in range(N):
   p=low+(np.array([x,y,z])+.5)*size/N
   hit,normal,idx,d=bvh.find_nearest(Vector(p));signed=(Vector(p)-hit).dot(normal)
   field[z,y,x]=d if signed>=0 else -d
 if z%24==0:print('SDF slice',z,flush=True)
with (out/'Ethan_HeadSdf.bytes').open('wb') as f:
 f.write(struct.pack('<i6f',N,*(list((low+high)/2)+list(size))));f.write(field.astype('<f4').tobytes())
report={'source':'testing.blend','curves':len(result),'particles_per_strand':32,'curve_types':np.unique(types).tolist(),'skull_registration_median_mm':float(np.median(errors)*1000),'registration_rotation':R.tolist(),'registration_translation':t.tolist(),'roots_lifted':corrected,'sdf_resolution':N,'sdf_bounds':[low.tolist(),high.tolist()],'sdf_sign_method':'nearest oriented surface; valid around scalp and ears, not a watertight mouth/interior test'}
(root/'Temp/fitted_groom_build.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
