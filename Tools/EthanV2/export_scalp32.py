import bpy,numpy as np,pathlib,json,struct,hashlib
from mathutils.bvhtree import BVHTree
from mathutils import Vector
R=pathlib.Path('D:/UnityProject/MetahumanTemplate');O=R/'Data/EthanV2/Groom';O.mkdir(exist_ok=True)
audit=json.loads((O/'audit.json').read_text());hm=np.array(audit['bones'][0]['matrix']).reshape(4,4);hm[:3,:3]/=np.linalg.norm(hm[:3,:3],axis=0);inv=np.linalg.inv(hm)
head=bpy.data.objects['BAKED_head_lod0_mesh'];head.data.calc_loop_triangles();tri=[list(reversed(t.vertices)) for t in head.data.loop_triangles if max(t.vertices)<19250]
a=np.empty(len(head.data.vertices)*3,np.float32);head.data.vertices.foreach_get('co',a);v=a.reshape(-1,3)[:19250];m=np.array(head.matrix_world);v=v@m[:3,:3].T+m[:3,3]
def unity(p):return np.stack([-p[:,0],p[:,2],-p[:,1]],axis=1)
v=unity(v)@inv[:3,:3].T+inv[:3,3]
g=bpy.data.objects['BAKED_Groom_01_Center_part_curtains'];p=np.empty(len(g.data.points)*3,np.float32);g.data.attributes['position'].data.foreach_get('vector',p);p=p.reshape(-1,3);gm=np.array(g.matrix_world);reference=p@gm[:3,:3].T+gm[:3,3]
before=set(bpy.data.objects);src=pathlib.Path('C:/Users/ina17/Downloads/Ethan_Ver2_NewMetaHumanCharacter/Ethan_NewMetaHumanCharacter/groom/Hair_Overall.abc');bpy.ops.wm.alembic_import(filepath=str(src),as_background_job=False)
abc=next(o for o in set(bpy.data.objects)-before if o.type=='CURVES' and len(o.data.curves)==94408)
raw=np.empty(len(abc.data.points)*3,np.float32);abc.data.attributes['position'].data.foreach_get('vector',raw);raw=raw.reshape(-1,3).astype(float);am=np.array(abc.matrix_world);raw=raw@am[:3,:3].T+am[:3,3]
assert len(raw)==len(reference)
sample=np.linspace(0,len(raw)-1,20000,dtype=int);x=raw[sample];y=reference[sample];mx=x.mean(0);my=y.mean(0);u,s,vt=np.linalg.svd((x-mx).T@(y-my));q=u@vt;scale=s.sum()/np.square(x-mx).sum();world=(raw-mx)@q*scale+my
match=float(np.linalg.norm(world-reference,axis=1).max());assert match<1e-4,match
world=unity(world);points=world@inv[:3,:3].T+inv[:3,3];curves=[]
for c in abc.data.curves:
 p=points[c.first_point_index:c.first_point_index+c.points_length];d=np.r_[0,np.cumsum(np.linalg.norm(np.diff(p,axis=0),axis=1))];t=np.linspace(0,d[-1],32);curves.append(np.stack([np.interp(t,d,p[:,i]) for i in range(3)],axis=1))
curves=np.array(curves,dtype=np.float32)
dest=R/'Assets/_Sandbox/Characters/Ethan_V2/Groom';dest.mkdir(exist_ok=True)
with (dest/'EthanV2_Scalp32.bytes').open('wb') as f:f.write(struct.pack('<iii',0x48414952,len(curves),32));f.write(curves.tobytes())
# Fitted neutral head SDF in the same normalized head frame; x varies fastest.
bvh=BVHTree.FromPolygons(v.tolist(),tri,all_triangles=True);lo=v.min(0)-.025;hi=v.max(0)+.025;n=96;volume=np.empty((n,n,n),np.float32)
for z in range(n):
 for y in range(n):
  for x in range(n):
   p=lo+(hi-lo)*(np.array([x,y,z])+.5)/n;hit,normal,index,d=bvh.find_nearest(Vector(p));volume[z,y,x]=d if np.dot(p-np.array(hit),np.array(normal))>=0 else -d
(dest/'EthanV2_HeadSDF.bytes').write_bytes(volume.tobytes())
report={'source':str(src),'source_sha256':hashlib.sha256(src.read_bytes()).hexdigest(),'scalp_object':abc.name,'strands':len(curves),'points_per_strand':32,'max_abc_to_blend_correspondence_m':match,'unit_conversion_scale':float(scale),'normalized_head_matrix':hm.tolist(),'head_bone_path':audit['bones'][0]['path'],'sdf_resolution':n,'sdf_min':lo.tolist(),'sdf_max':hi.tolist(),'root_tip_preserved':True,'bounds':[curves.reshape(-1,3).min(0).tolist(),curves.reshape(-1,3).max(0).tolist()]}
(O/'export.json').write_text(json.dumps(report,indent=2));print('SCALP_EXPORTED',json.dumps(report),flush=True)
