"""Bake 73 project shapes from original Polyhammer RigLogic, preserving skinning.

Run on Liora_FaceBoard_Restored.blend with the installed addon enabled.
Reopen the resulting file with --factory-startup --disable-autoexec and append
-- --verify to independently compare every shape with saved solver samples.
"""
import bpy, pathlib, sys, json, hashlib, importlib, numpy as np
from mathutils import Matrix
sys.path.insert(0,str(pathlib.Path(__file__).parent))
from native_pose_mapping import make_mapping

ROOT=pathlib.Path('D:/UnityProject/MetahumanTemplate');OUT=ROOT/'Data/Liora/Native'
PREPARED=OUT/'Liora_73_Baked.blend'
TOL=2e-6
def coords(data):
    a=np.empty(len(data)*3,dtype=np.float32);data.foreach_get('co',a);return a.reshape(-1,3).astype(np.float64)
def evaluated(obj):
    ev=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());m=ev.to_mesh()
    try:
        assert len(m.vertices)==len(obj.data.vertices),obj.name+': topology changed'
        return coords(m.vertices)
    finally:ev.to_mesh_clear()
def update():bpy.context.view_layer.update()
def distance(obj,a,b):return float(np.linalg.norm((a-b)@np.array(obj.matrix_world.to_3x3()).T,axis=1).max())
def weight_hash(obj):
    h=hashlib.sha256()
    for v in obj.data.vertices:
        for g in v.groups:
            h.update(np.array([v.index,g.group],np.int32).tobytes());h.update(np.float32(g.weight).tobytes())
    return h.hexdigest()
def groom_hash(obj):
    a=np.empty(len(obj.data.points)*3,np.float32);obj.data.attributes['position'].data.foreach_get('vector',a)
    return hashlib.sha256(a.tobytes()).hexdigest()

def verify():
    report=json.loads((OUT/'bake_report.json').read_text());expected=np.load(OUT/'evaluated_baselines.npz')
    meshes=[bpy.data.objects[m['baked']] for m in report['meshes']]
    face=bpy.data.objects[report['primary_face']]
    for obj,m in zip(meshes,report['meshes']):
        assert weight_hash(obj)==m['weights_sha256']
        assert [k.name for k in obj.data.shape_keys.key_blocks]==['Basis']+report['shape_names']
        assert all(k.value==0 for k in obj.data.shape_keys.key_blocks)
    maximum=0;comparisons=0
    for name in ['Basis']+report['shape_names']:
        if name!='Basis':face.data.shape_keys.key_blocks[name].value=1
        update()
        for obj in meshes:
            err=distance(obj,evaluated(obj),expected[obj.name+'|'+name]);maximum=max(maximum,err);comparisons+=1
            assert err<TOL,(obj.name,name,err)
        if name!='Basis':face.data.shape_keys.key_blocks[name].value=0
        update()
    for obj in meshes:assert distance(obj,evaluated(obj),expected[obj.name+'|Basis'])<TOL
    for item in report['grooms']:
        obj=bpy.data.objects[item['baked']]
        assert groom_hash(obj)==item['points_sha256']
        assert len(obj.data.curves)==item['curves'] and len(obj.data.points)==item['points']
        assert np.max(np.abs(np.array(obj.matrix_world)-np.array(item['matrix_world'])))<1e-6
    result={'passed':True,'mesh_shape_comparisons_including_neutral':comparisons,'max_error_m':maximum,'ordered_73_names':True,'weights_preserved':True,'neutral_restored':True,'groom_points_and_frames_preserved':True,'verified_without_addon':True}
    (OUT/'reopen_verification.json').write_text(json.dumps(result,indent=2));print('VERIFY_RESULT',json.dumps(result),flush=True)

def bake():
    assert not PREPARED.exists(),'Refuse to overwrite an existing bake'
    A='bl_ext.api_portal_polyhammer_com_001.character_dna'
    u=importlib.import_module(A+'.utilities');controller=importlib.import_module(A+'.runtime.controller');authoring=importlib.import_module(A+'.runtime.authoring')
    inst=u.get_addon_scene_properties().rig_instance_list[0]
    inst.initialize();controller.rebuild(inst);controller.suspend(inst)
    reader=inst.head_dna_reader
    mapping,provenance=make_mapping([reader.getGUIControlName(i) for i in range(reader.getGUIControlCount())])
    names=json.loads((OUT.parent/'shape_names.json').read_text());assert len(names)==73 and set(names)==set(mapping)
    board=inst.face_board
    base={b.name:b.matrix_basis.copy() for b in board.pose.bones}
    gui=[reader.getGUIControlName(i).rsplit('.',1) for i in range(reader.getGUIControlCount())]
    def pose(controls):
        for b in board.pose.bones:b.matrix_basis=base[b.name]
        for bone,channel in gui:board.pose.bones[bone].location['xyz'.index(channel[-1])]=0
        board.pose.bones['CTRL_lookAtSwitch'].location.y=0
        for bone,values in controls.items():
            for axis,value in values.items():board.pose.bones[bone].location['xyz'.index(axis)]=value
        board.update_tag();update()
        authoring.sample(inst,'head')
        authoring.bone_transforms(inst,'head')
        inst.update_head_shape_keys();inst.head_rig.update_tag();update()
    pose({})
    objects=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith(inst.name+'_')]
    source_collection=bpy.data.collections.new('SOURCE_AUTHORING - preserved, hidden');bpy.context.scene.collection.children.link(source_collection)
    for obj in list(bpy.context.scene.objects):
        for c in list(obj.users_collection):c.objects.unlink(obj)
        source_collection.objects.link(obj)
    baked_collection=bpy.data.collections.new('LIORA_BAKED - select head shape keys');bpy.context.scene.collection.children.link(baked_collection)
    armatures={m.object for o in objects for m in o.modifiers if m.type=='ARMATURE' and m.object}
    frozen={}
    for original in armatures:
        clone=original.copy();clone.data=original.data.copy();clone.name='BAKED_'+original.name
        clone.animation_data_clear();clone.parent=None;clone.matrix_world=original.matrix_world.copy();baked_collection.objects.link(clone)
        matrices={b.name:b.matrix.copy() for b in original.evaluated_get(bpy.context.evaluated_depsgraph_get()).pose.bones}
        for b in clone.pose.bones:
            for c in list(b.constraints):b.constraints.remove(c)
        update()
        for b in clone.pose.bones:
            options={'parent_matrix':matrices[b.parent.name],'parent_matrix_local':b.parent.bone.matrix_local} if b.parent else {}
            b.matrix_basis=b.bone.convert_local_to_pose(matrices[b.name],b.bone.matrix_local,invert=True,**options)
        update();clone.hide_render=True;clone.hide_set(True);frozen[original]=clone
    clones={};neutral={};raw={};inverse={};mesh_report=[];expected={}
    for obj in objects:
        clone=obj.copy();clone.data=obj.data.copy();clone.name='BAKED_'+obj.name.removeprefix(inst.name+'_');clone.animation_data_clear()
        if clone.data.shape_keys:clone.shape_key_clear()
        clone.parent=frozen.get(obj.parent);clone.parent_type='OBJECT';baked_collection.objects.link(clone);clone.matrix_world=obj.matrix_world.copy()
        for m in clone.modifiers:
            if m.type=='ARMATURE' and m.object:m.object=frozen[m.object]
        clone.hide_set(False);clones[obj]=clone;raw[obj]=coords(obj.data.vertices);update()
        neutral[obj]=evaluated(obj);frozen_neutral=evaluated(clone)
        err=distance(obj,frozen_neutral,neutral[obj]);assert err<.0001,(obj.name,'freeze',err)
        basis=clone.shape_key_add(name='Basis');basis.value=0;probe=clone.shape_key_add(name='_calibration');columns=[]
        for axis in range(3):
            offset=np.zeros(3);offset[axis]=.01
            probe.data.foreach_set('co',(raw[obj]+offset).astype(np.float32).ravel());clone.data.update();probe.value=1;update()
            columns.append((evaluated(clone)-frozen_neutral)/.01)
        probe.value=0;clone.shape_key_remove(probe);inverse[obj]=np.linalg.inv(np.stack(columns,axis=2))
        raw[obj]+=np.einsum('nij,nj->ni',inverse[obj],neutral[obj]-frozen_neutral)
        basis.data.foreach_set('co',raw[obj].astype(np.float32).ravel());clone.data.update();update()
        assert distance(clone,evaluated(clone),neutral[obj])<TOL
        expected[clone.name+'|Basis']=neutral[obj].astype(np.float32)
        mesh_report.append({'source':obj.name,'baked':clone.name,'vertices':len(obj.data.vertices),'weights_sha256':weight_hash(obj),'frozen_neutral_error_m':err,'materials':[m.name if m else None for m in obj.data.materials]})
    shapes=[];zero_shapes=[]
    for name in names:
        close_reference=None
        if name=='mouthClose':
            pose({'CTRL_C_jaw':{'y':1}})
            close_reference={obj:evaluated(obj) for obj in objects}
        pose(mapping[name]);maxima={}
        for obj,clone in clones.items():
            sampled=evaluated(obj)
            if close_reference is not None:sampled=sampled-close_reference[obj]+neutral[obj]
            delta=sampled-neutral[obj]
            key=clone.shape_key_add(name=name);key.value=0
            target=raw[obj]+np.einsum('nij,nj->ni',inverse[obj],delta);assert np.isfinite(target).all()
            key.data.foreach_set('co',target.astype(np.float32).ravel());clone.data.update()
            maxima[clone.name]=distance(obj,sampled,neutral[obj]);expected[clone.name+'|'+name]=sampled.astype(np.float32)
        maximum=max(maxima.values())
        if name!='viseme_sil' and maximum<=1e-6:zero_shapes.append(name)
        if name=='viseme_sil':assert maximum<TOL
        shapes.append({'name':name,'max_displacement_m':maximum,'per_mesh_displacement_m':maxima,'pose_source':provenance[name],'subtract_reference_pose':{'CTRL_C_jaw':{'y':1}} if name=='mouthClose' else None})
        print('BAKED_NATIVE',name,'max_mm',round(maximum*1000,4),flush=True)
    (OUT/'shape_preflight.json').write_text(json.dumps({'zero_shapes':zero_shapes,'shapes':shapes},indent=2))
    assert not zero_shapes,('No deformation',zero_shapes)
    pose({})
    for obj in objects:assert distance(obj,evaluated(obj),neutral[obj])<TOL
    face=clones[inst.head_mesh]
    # One head slider synchronizes all mouth, eye, eyelash, teeth and saliva parts.
    for obj in clones.values():
        if obj==face:continue
        for name in names:
            driver=obj.data.shape_keys.key_blocks[name].driver_add('value').driver;driver.type='SUM'
            var=driver.variables.new();var.name='head_value';var.type='SINGLE_PROP';var.targets[0].id_type='KEY';var.targets[0].id=face.data.shape_keys
            var.targets[0].data_path='key_blocks['+json.dumps(name)+'].value'
    groom_report=[]
    for original in [o for o in source_collection.objects if o.type=='CURVES']:
        clone=original.copy();clone.data=original.data.copy();clone.name='BAKED_Groom_'+original.name;baked_collection.objects.link(clone)
        clone.parent=face;clone.parent_type='OBJECT';clone.parent_bone='';clone.matrix_world=original.matrix_world.copy()
        if clone.data.surface in clones:clone.data.surface=clones[clone.data.surface]
        clone.hide_set(False)
        groom_report.append({'source':original.name,'baked':clone.name,'curves':len(original.data.curves),'points':len(original.data.points),'points_sha256':groom_hash(original),'matrix_world':[list(row) for row in original.matrix_world],'surface_follows_shapes':bool(clone.data.surface)})
        assert groom_hash(clone)==groom_hash(original)
    source_collection.hide_render=True;source_collection.hide_viewport=True
    for obj in source_collection.objects:
        if obj.animation_data:
            for driver in obj.animation_data.drivers:driver.mute=True
    # Saved baked file is independent of the addon. The restored authoring file
    # remains separately available with the fully connected original face board.
    u.get_addon_scene_properties().rig_instance_list.clear()
    for obj in bpy.context.selected_objects:obj.select_set(False)
    face.hide_set(False);face.select_set(True);bpy.context.view_layer.objects.active=face;face.active_shape_key_index=0
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    bpy.context.scene.render.fps=30
    report={'prepared':str(PREPARED),'source':'Liora_FaceBoard_Restored.blend','method':'Original Polyhammer native RigLogic GUI solving, bone motion plus restored DNA correctives; inverse-neutral-stack baking into unchanged weights.','primary_face':face.name,'shape_names':names,'meshes':mesh_report,'shapes':shapes,'grooms':groom_report,'corrective_mapping_report':'corrective_restoration.json','visual_review_required':True,'limitations':['73 names are mapped to native controls and Polyhammer phoneme/emotion presets; they are not copied other-character vertex deltas or recovered motion clips.','The split head uses nearest neutral DNA vertices (maximum 0.27123 mm matching distance); inspect inner lips and tongue.','Groom source points are preserved; this is not a Unity groom import or simulation change.','Do not pose frozen facial bones on top of their corresponding baked shape keys.','Artist review of closure, gaze, contact and combined shapes remains necessary.']}
    (OUT/'shape_pose_mapping.json').write_text(json.dumps({'mapping':mapping,'provenance':provenance,'subtract_reference_pose':{'mouthClose':{'CTRL_C_jaw':{'y':1}}}},indent=2))
    (OUT/'bake_report.json').write_text(json.dumps(report,indent=2));np.savez_compressed(OUT/'evaluated_baselines.npz',**expected)
    bpy.ops.wm.save_as_mainfile(filepath=str(PREPARED));print('NATIVE_BAKE_SAVED',str(PREPARED),flush=True)

if '--verify' in sys.argv:verify()
else:bake()


