"""Fit Liora's garment with body-surface weights and bounded local clearance.
Uses saved disposable Unity poses; never changes Face or removes skin.
"""
import json,struct,pathlib,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
R=pathlib.Path('D:/UnityProject/MetahumanTemplate/Data/Liora/ClothingFit')
d=json.loads((R/'binding_input.json').read_text());poses=json.loads((R/'samples.json').read_text())
with (R/'samples.bin').open('rb') as f:nb,nc,nf,no=struct.unpack('<iiii',f.read(16))
width=nb*3+nc*3+(nf+no)*16
raw=np.memmap(R/'samples.bin',dtype='<f4',offset=16,shape=(len(poses),width))
body=raw[:,:nb*3].reshape(-1,nb,3);oldcloth=raw[:,nb*3:nb*3+nc*3].reshape(-1,nc,3)
face_m=raw[:,nb*3+nc*3:nb*3+nc*3+nf*16].reshape(-1,nf,4,4).astype(float)
old_m=raw[:,nb*3+nc*3+nf*16:].reshape(-1,no,4,4).astype(float)
X=np.array(d['clothToFace']).reshape(4,4);new_m=face_m@X
all_m=np.concatenate([old_m,new_m],axis=1)
b=np.array(d['bodyVertices']);c=np.array(d['clothVertices']);ct=np.array(d['clothTriangles']).reshape(-1,3);bt=np.array(d['bodyTriangles']).reshape(-1,3)
def weights(counts,values):
 result=[];at=0
 for n in counts:result.append({v['bone']:v['weight'] for v in values[at:at+n]});at+=n
 return result
bw=weights(d['bodyCounts'],d['bodyWeights']);cw=weights(d['clothCounts'],d['clothWeights'])
# Restrict correspondence to torso/neck/shoulders, excluding forearms and hands
# which can approach the chest during Thinking without belonging under the shirt.
limbs=np.array([sum(w for index,w in ws.items() if any(part in d['bodyBoneNames'][index] for part in ['hand_','lowerarm_','index_','middle_','ring_','pinky_','thumb_'])) for ws in bw])
centers=b[bt].mean(1);keep=(centers[:,1]>c[:,1].min()-.04)&(centers[:,1]<c[:,1].max()+.025)&(np.abs(centers[:,0])<.23)&(limbs[bt].mean(1)<.15)
bt=bt[keep];surface=BVHTree.FromPolygons(b.tolist(),bt.tolist(),all_triangles=True)
radial=centers[keep].copy();radial[:,1]=0;cross=np.cross(b[bt[:,1]]-b[bt[:,0]],b[bt[:,2]]-b[bt[:,0]])
if np.sum(cross*radial)<0:bt=bt[:,::-1];surface=BVHTree.FromPolygons(b.tolist(),bt.tolist(),all_triangles=True)
def ramp(a,z,x):
 t=np.clip((x-a)/(z-a),0,1);return t*t*(3-2*t)
alpha=np.ones(nc)
cache={};rows=[];anchors=[];distances=[]
for index,point in enumerate(c):
 key=tuple(np.round(point,5))
 if key not in cache:
  hit,normal,tri,distance=surface.find_nearest(Vector(point));verts=bt[tri];v=b[verts];uv=np.linalg.lstsq(np.stack([v[1]-v[0],v[2]-v[0]],axis=1),np.array(hit)-v[0],rcond=None)[0];bary=np.maximum(0,[1-uv.sum(),*uv]);bary/=bary.sum();ws={}
  for vertex,f in zip(verts,bary):
   for bone,w in bw[vertex].items():ws[no+bone]=ws.get(no+bone,0)+float(f)*w
  cache[key]=(ws,{'triangle':verts.tolist(),'bary':bary.tolist()},distance)
 transferred,anchor,distance=cache[key];merged={bone:w*(1-alpha[index]) for bone,w in cw[index].items()}
 for bone,w in transferred.items():merged[bone]=merged.get(bone,0)+w*alpha[index]
 values=sorted([(bone,w) for bone,w in merged.items() if w>1e-8],key=lambda v:-v[1])[:8];total=sum(v[1] for v in values);rows.append(list(cw[index].items()) if alpha[index]==0 else [(int(bone),float(w/total)) for bone,w in values]);anchors.append(anchor);distances.append(distance)
indices=np.zeros((nc,8),np.int32);values=np.zeros((nc,8))
for j,row in enumerate(rows):
 for k,(index,w) in enumerate(row):indices[j,k]=index;values[j,k]=w
def weighted(m):return np.einsum('nk,nkij->nij',values,m[indices])
neutral_matrix=weighted(all_m[0]);neutral_inverse=np.linalg.inv(neutral_matrix)
local=(neutral_inverse@np.c_[c,np.ones(nc)][:,:,None])[:,:3,0]
local[alpha==0]=np.array(d['clothRawVertices'])[alpha==0]
base_local=local.copy();matrices=np.array([weighted(m) for m in all_m]);inverse_linear=np.linalg.inv(matrices[:,:,:3,:3])
_,seam_group=np.unique(np.round(c,5),axis=0,return_inverse=True);seam_counts=np.bincount(seam_group)
def evaluate(local):return np.einsum('snij,nj->sni',matrices[:,:,:3,:3],local)+matrices[:,:,:3,3]
transferred=evaluate(local)
print('WEIGHT_TRANSFER',nc,'affected',int((alpha>0).sum()),'neutral_error',float(np.linalg.norm(transferred[0]-c,axis=1).max()),flush=True)
bvhs=[BVHTree.FromPolygons(points.tolist(),bt.tolist(),all_triangles=True) for points in body]
# Probe garment vertices and every triangle center/edge midpoint.
probe_i=np.concatenate([np.repeat(np.arange(nc)[:,None],3,axis=1),ct,ct,ct,ct])
probe_b=np.concatenate([np.tile([1.,0,0],(nc,1)),np.tile([1/3]*3,(len(ct),1)),np.tile([.5,.5,0],(len(ct),1)),np.tile([0,.5,.5],(len(ct),1)),np.tile([.5,0,.5],(len(ct),1))])
probe_mask=(alpha[probe_i]*probe_b).sum(1)
def contacts(points,bvh,details=False):
 pp=(points[probe_i]*probe_b[:,:,None]).sum(1);hits=[];worst=0;count=0
 for j,point in enumerate(pp):
  if probe_mask[j]<=0:continue
  hit,n,tri,dist=bvh.find_nearest(Vector(point))
  if dist>.035:continue
  normal=np.array(n);signed=float(np.dot(point-np.array(hit),normal))
  if signed<-.0005:count+=1;worst=min(worst,signed)
  if details and signed<.0015 and dist<.02:hits.append((j,normal,min(.004,.0015-signed)))
 return count,worst,hits
before_metrics=[];weight_metrics=[]
for j,pose in enumerate(poses):
 a,w,_=contacts(oldcloth[j],bvhs[j]);b0,w0,_=contacts(transferred[j],bvhs[j]);before_metrics.append({'pose':j,'count':a,'min_signed_m':w});weight_metrics.append({'pose':j,'count':b0,'min_signed_m':w0})
print('CONTACTS_BASELINE',sum(x['count'] for x in before_metrics),'WEIGHTS_ONLY',sum(x['count'] for x in weight_metrics),flush=True)
iterations=[]
for iteration in range(5):
 fitted=evaluate(local);push=np.zeros_like(local);denom=np.zeros(nc);total=0
 for j in range(len(poses)):
  count,worst,hits=contacts(fitted[j],bvhs[j],True);total+=count
  for probe,normal,distance in hits:
   for vertex,f in zip(probe_i[probe],probe_b[probe]):
    if f<=0 or alpha[vertex]<=0:continue
    correction=inverse_linear[j,vertex]@(normal*distance)
    push[vertex]+=correction*f;denom[vertex]+=f
 active=denom>0
 if not active.any():break
 push[active]/=denom[active,None];push*=alpha[:,None]
 local+=push*.8
 # Bound accumulated neutral displacement relative to the original garment.
 displacement=np.einsum('nij,nj->ni',neutral_matrix[:,:3,:3],local-base_local)
 shared=np.zeros((len(seam_counts),3));np.add.at(shared,seam_group,displacement);displacement=shared[seam_group]/seam_counts[seam_group,None]
 length=np.linalg.norm(displacement,axis=1);factor=np.minimum(1,.010/np.maximum(length,1e-15));displacement*=factor[:,None];local=base_local+np.einsum('nij,nj->ni',neutral_inverse[:,:3,:3],displacement)
 iterations.append({'iteration':iteration+1,'pre_step_contacts':total,'max_accumulated_neutral_displacement_m':float(np.linalg.norm(np.einsum('nij,nj->ni',neutral_matrix[:,:3,:3],local-base_local),axis=1).max())});print('CLEARANCE_PASS',iterations[-1],flush=True)
fitted=evaluate(local);after=[]
for j in range(len(poses)):
 count,worst,_=contacts(fitted[j],bvhs[j]);after.append({'pose':j,'count':count,'min_signed_m':worst})
displacement=fitted[0]-c
report={'region':'Entire garment rebound to the native torso/neck surface after the first local transfer left waist breakthroughs','weighted_vertices_changed':int((alpha>0).sum()),'max_accumulated_neutral_displacement_m':float(np.linalg.norm(displacement,axis=1).max()),'mean_displacement_m':float(np.linalg.norm(displacement,axis=1).mean()),'poses':len(poses),'probes_per_pose':len(probe_i),'surface_triangles':len(bt),'baseline':before_metrics,'weights_only':weight_metrics,'after':after,'iterations':iterations,'contact_definition':'Garment vertices, centers and edge midpoints with nearest torso/neck surface normal distance below -0.5mm, within 35mm. These are contact proxies, not guaranteed signed penetration depths of closed volumes.','no_body_masking':True}
(R/'fit_report.json').write_text(json.dumps(report,indent=2));(R/'fitted_weights.json').write_text(json.dumps({'weights':rows,'rawVertices':local.tolist(),'blend':alpha.tolist(),'anchors':anchors,'clearanceWorldNeutral':displacement.tolist()}));fitted.astype('<f4').tofile(R/'predicted_fitted.bin');transferred.astype('<f4').tofile(R/'predicted_weights_only.bin');bt.astype('<i4').tofile(R/'surface_triangles.bin')
print('FIT_COMPLETE',json.dumps({k:v for k,v in report.items() if k not in ['baseline','weights_only','after','iterations']}),'contacts',sum(x['count'] for x in after),flush=True)

