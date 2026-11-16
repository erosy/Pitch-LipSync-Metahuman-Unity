import bpy,numpy as np,pathlib,json,struct
root=pathlib.Path('D:/UnityProject/MetahumanTemplate');out=root/'Data/EthanV2/HairComparison';out.mkdir(parents=True,exist_ok=True)
path=root/'Assets/_Sandbox/Groom Hair Asset/Fitted Male/Ethan_BlenderFitted.bytes'
with path.open('rb') as f:
 magic,n,res=struct.unpack('<iii',f.read(12));old=np.frombuffer(f.read(),dtype='<f4').reshape(n,res,3).astype(float)
o=bpy.data.objects['BAKED_Groom_01_Center_part_curtains']
a=np.empty(len(o.data.points)*3,np.float32);o.data.attributes['position'].data.foreach_get('vector',a);a=a.reshape(-1,3).astype(float)
m=np.array(o.matrix_world);a=a@m[:3,:3].T+m[:3,3]
new=[]
for c in o.data.curves:
 p=a[c.first_point_index:c.first_point_index+c.points_length];arc=np.r_[0,np.cumsum(np.linalg.norm(np.diff(p,axis=0),axis=1))];s=np.linspace(0,arc[-1],res)
 new.append(np.stack([np.interp(s,arc,p[:,i]) for i in range(3)],axis=1))
new=np.array(new);assert len(new)==n
indices=np.linspace(0,n-1,4096,dtype=int);x=new[indices,::4].reshape(-1,3);y=old[indices,::4].reshape(-1,3)
mx=x.mean(0);my=y.mean(0);u,s,vt=np.linalg.svd((x-mx).T@(y-my));rotation=u@vt
aligned=(new-mx)@rotation+my
errors=np.linalg.norm(aligned-old,axis=2);oldlen=np.linalg.norm(np.diff(old,axis=1),axis=2).sum(1);newlen=np.linalg.norm(np.diff(new,axis=1),axis=2).sum(1)
def stats(a):return {'min':float(a.min()),'median':float(np.median(a)),'p95':float(np.quantile(a,.95)),'max':float(a.max())}
report={'new_groom':o.name,'old_asset':str(path),'strands_old':n,'strands_new':len(new),'old_samples':res,'new_authored_points':len(a),'comparison_samples':res,'ordered_comparison_after_rigid_alignment_no_scaling':True,'reflection_allowed_for_coordinate_conversion':True,'point_error_mm':stats(errors*1000),'root_error_mm':stats(errors[:,0]*1000),'tip_error_mm':stats(errors[:,-1]*1000),'old_length_mm':stats(oldlen*1000),'new_length_mm':stats(newlen*1000),'paired_length_error_mm':stats(np.abs(oldlen-newlen)*1000),'paired_length_correlation':float(np.corrcoef(oldlen,newlen)[0,1]),'notes':['New CURVES control polygons resampled by arc length for comparison; old production samples include skull registration and root clearance corrections.','Rigid fit removes coordinate orientation/position only, with no uniform scale or reshaping.']}
(out/'comparison.json').write_text(json.dumps(report,indent=2));print('HAIR_COMPARISON',json.dumps(report),flush=True)
