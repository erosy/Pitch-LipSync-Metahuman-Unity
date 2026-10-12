"""Run with Blender --background --factory-startup --python. Original groom stays intact."""
import json, pathlib, struct
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = pathlib.Path(__file__).resolve().parents[2]
FOLDER = ROOT / 'Assets/_Sandbox/Groom Hair Asset/Fitted'
data = np.fromfile(FOLDER / 'Ethan_BlenderFitted.bytes', dtype='<f4', offset=12).reshape(-1, 32, 3)
original = data.copy()
tip=data[:,-1]; roots=data[:,0]
trim_ids=np.where((np.abs(tip[:,0])>.06)&(np.abs(tip[:,0])<.105)&(tip[:,1]>.005)&(tip[:,1]<.095)&(tip[:,2]>-.065)&(tip[:,2]<.035)&(roots[:,1]>.115))[0]
for strand in trim_ids:
    curve=data[strand].copy()
    crossings=np.where((curve[:-1,1]>=.105)&(curve[1:,1]<.105))[0]
    if not len(crossings): continue
    k=int(crossings[-1]);t=(curve[k,1]-.105)/(curve[k,1]-curve[k+1,1])
    clipped=np.vstack([curve[:k+1],curve[k]+t*(curve[k+1]-curve[k])])
    lengths=np.r_[0,np.cumsum(np.linalg.norm(np.diff(clipped,axis=0),axis=1))]
    target=np.linspace(0,lengths[-1],32)
    data[strand]=np.column_stack([np.interp(target,lengths,clipped[:,a])for a in range(3)])
with (FOLDER / 'Ethan_HeadSdf.bytes').open('rb') as stream:
    n, *bounds = struct.unpack('<i6f', stream.read(28))
    field = np.frombuffer(stream.read(), dtype='<f4').reshape(n, n, n)
center, size = np.array(bounds[:3]), np.array(bounds[3:])
low = center - size*.5

def sample(points):
    coord = np.clip((points-low)/size*n-.5, 0, n-1.00001)
    lo = coord.astype(int); weight = coord-lo
    out = np.zeros(points.shape[:-1])
    for z in (0, 1):
        for y in (0, 1):
            for x in (0, 1):
                out += field[lo[...,2]+z,lo[...,1]+y,lo[...,0]+x] * (weight[...,0] if x else 1-weight[...,0]) * (weight[...,1] if y else 1-weight[...,1]) * (weight[...,2] if z else 1-weight[...,2])
    return out

region = (np.abs(data[...,0])>.075)&(data[...,1]>.005)&(data[...,1]<.165)&(data[...,2]>-.075)&(data[...,2]<.035)
distance = sample(data)
# Contact also begins outside the mesh, within the solver's 3.5 mm safety margin.
affected = np.where(np.any(region & (distance<.0035) & (np.arange(32)[None,:]>=3),axis=1))[0]
head = json.loads((ROOT/'Temp/ethan_head_local.json').read_text())
mesh = BVHTree.FromPolygons(head['vertices'],np.array(head['triangles']).reshape(-1,3).tolist(),all_triangles=True)
checks=[]
for strand in affected[::max(1,len(affected)//100)]:
    j = int(np.argmin(np.where(region[strand],distance[strand],1)))
    p = Vector(data[strand,j]);hit,normal,_,d=mesh.find_nearest(p)
    checks.append({'strand':int(strand),'point':j,'fieldMm':float(distance[strand,j]*1000),'meshSignedMm':float((p-hit).dot(normal)*1000)})

curves=data[affected].astype(float)
rest=curves.copy(); localRegion=region[affected]
# Spread correction over neighboring particles to avoid a sharp bend.
active=localRegion.copy()
for _ in range(3): active[:,1:] |= active[:,:-1]; active[:,:-1] |= active[:,1:]
active[:,:3]=False
project=localRegion.copy(); project[:,:3]=False
epsilon=.0005
for iteration in range(40):
    dist=sample(curves)
    grad=np.stack([(sample(curves+np.eye(3)[k]*epsilon)-sample(curves-np.eye(3)[k]*epsilon))/(2*epsilon) for k in range(3)],axis=-1)
    length=np.linalg.norm(grad,axis=-1)
    grad/=np.maximum(length[...,None],.1)
    delta=grad*np.maximum(0,.004-dist)[...,None]*project[...,None]
    curves+=delta*.8
    displacement=curves-rest
    smoothed=displacement.copy()
    smoothed[:,1:-1]=.25*displacement[:,:-2]+.5*displacement[:,1:-1]+.25*displacement[:,2:]
    curves=rest+smoothed*active[...,None]
    curves[:,:3]=rest[:,:3]
# Final bounded projection removes residual smoothing-induced penetration.
for _ in range(12):
    dist=sample(curves)
    grad=np.stack([(sample(curves+np.eye(3)[k]*epsilon)-sample(curves-np.eye(3)[k]*epsilon))/(2*epsilon) for k in range(3)],axis=-1)
    length2=np.sum(grad*grad,axis=-1)
    curves+=grad*(np.maximum(0,.004-dist)/np.maximum(length2,.25))[...,None]*project[...,None]*.8
    curves[:,:3]=rest[:,:3]
movement=np.linalg.norm(curves-rest,axis=-1)
if not np.isfinite(curves).all() or movement.max()>.020:
    i,j=np.unravel_index(np.argmax(movement),movement.shape)
    print({'worstStrand':int(affected[i]),'point':int(j),'original':rest[i,j].tolist(),'movedMm':float(movement[i,j]*1000),'originalFieldMm':float(sample(rest[i,j])*1000),'meshChecks':checks[:8]},flush=True)
    raise RuntimeError('Correction exceeds 20 mm bound or contains nonfinite data; do not import.')
data[affected]=curves
with (FOLDER/'Ethan_EarClearance.bytes').open('wb') as stream:
    stream.write(struct.pack('<iii',0x48414952,len(data),32));stream.write(data.astype('<f4').tobytes())
after=sample(data)
report={'affectedStrands':len(affected),'totalStrands':len(data),'particlesPerStrand':32,'clearanceMm':4,'maxCorrectionMm':float(movement.max()*1000),
        'rootsUnchanged':bool(np.array_equal(data[:,0],original[:,0])), 'outsideRegionStrandsUnchanged':bool(np.array_equal(data[np.setdiff1d(np.arange(len(data)),affected)],original[np.setdiff1d(np.arange(len(data)),affected)])),
        'penetratingEarPointsBefore':int(np.sum(region & (distance<-.0005))), 'penetratingEarPointsAfter':int(np.sum(region & (after<-.0005))),
        'belowMarginEarPointsBefore':int(np.sum(region & (distance<.0035))), 'belowMarginEarPointsAfter':int(np.sum(region & (after<.0035))), 'meshFieldChecks':checks}
folder=ROOT/'Docs/HairRendering/EarCorrection';folder.mkdir(exist_ok=True)
(folder/'Build.json').write_text(json.dumps(report,indent=2))
print({k:v for k,v in report.items() if k!='meshFieldChecks'},flush=True)
