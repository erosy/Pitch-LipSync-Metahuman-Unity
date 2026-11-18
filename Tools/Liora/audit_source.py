import bpy, json, pathlib
out = pathlib.Path('D:/UnityProject/MetahumanTemplate/Data/Liora')
out.mkdir(parents=True, exist_ok=True)
report = {'version': bpy.app.version_string, 'objects': [], 'actions': [a.name for a in bpy.data.actions], 'texts': [t.name for t in bpy.data.texts]}
for o in bpy.data.objects:
    d = {'name': o.name, 'type': o.type, 'parent': o.parent.name if o.parent else None, 'location': list(o.location), 'scale': list(o.scale), 'modifiers': [{'name': m.name, 'type': m.type, 'target': getattr(getattr(m,'object',None),'name',None)} for m in o.modifiers]}
    if o.type == 'MESH':
        d.update(vertices=len(o.data.vertices), shapes=[k.name for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else [], materials=[m.name if m else None for m in o.data.materials])
    if o.type == 'ARMATURE':
        d['bones'] = [{'name': p.name, 'head': list(p.head), 'tail': list(p.tail), 'location': list(p.location), 'props': {k: str(p[k]) for k in p.keys()}, 'constraints': [{'type': c.type, 'target': getattr(getattr(c,'target',None),'name',None), 'subtarget': getattr(c,'subtarget',None)} for c in p.constraints]} for p in o.pose.bones]
    report['objects'].append(d)
report['images'] = [{'name': i.name, 'path': i.filepath, 'packed': bool(i.packed_file)} for i in bpy.data.images]
report['drivers'] = [{'id': o.name, 'drivers': [{'path': f.data_path,'expression': f.driver.expression,'variables': [{'name': v.name,'targets': [{'id': getattr(t.id,'name',None),'path':t.data_path, 'bone': t.bone_target} for t in v.targets]} for v in f.driver.variables]} for f in o.animation_data.drivers]} for o in bpy.data.objects if o.animation_data and o.animation_data.drivers]
(out/'source_audit.json').write_text(json.dumps(report, indent=2))
print('AUDIT_SAVED', out/'source_audit.json')

