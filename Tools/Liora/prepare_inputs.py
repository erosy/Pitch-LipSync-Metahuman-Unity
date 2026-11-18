import bpy, pathlib, json, hashlib
ROOT=pathlib.Path('D:/UnityProject/MetahumanTemplate'); OUT=ROOT/'Data/Liora/Native'
sources=[pathlib.Path('C:/Users/ina17/Downloads/Liora Minji_Ver2'),pathlib.Path('C:/Users/ina17/Downloads/LioraMinji_DNA_NewMetaHumanCharacter')]
files=[p for root in sources for p in root.rglob('*') if p.is_file()]
report={'sources':[{'path':str(p),'bytes':p.stat().st_size,'sha256':hashlib.file_digest(p.open('rb'),'sha256').hexdigest()} for p in files], 'images':[]}
for image in bpy.data.images:
    if not image.packed_file and image.filepath:
        name=pathlib.PureWindowsPath(image.filepath).name
        matches=[p for p in files if p.name==name]
        if matches: image.filepath=str(matches[0])
    report['images'].append({'name':image.name,'path':image.filepath,'packed':bool(image.packed_file),'exists':pathlib.Path(bpy.path.abspath(image.filepath)).is_file() if image.filepath else False})
(OUT/'source_manifest.json').write_text(json.dumps(report,indent=2))
