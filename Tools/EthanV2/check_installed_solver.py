"""Inspect installed Character DNA in a disposable process; never save source/prefs."""
import bpy, json, pathlib, sys
result={'blender':bpy.app.version_string,'python':sys.version,'enabled_addons':[a.module for a in bpy.context.preferences.addons if 'character_dna' in a.module],'callback_registered':callable(bpy.app.driver_namespace.get('character_dna_native_solve_v1')),'runtime':[],'dna_paths':[]}
for name,module in list(sys.modules.items()):
    if 'character_dna' in name and name.endswith('.runtime.engine'):
        try:capability=module.capability()
        except Exception as e:capability=[False,str(e)]
        result['runtime'].append({'module':name,'capability':capability,'errors':getattr(module,'_errors',{})})
for o in bpy.data.objects:
    if o.get('character_dna_native_carrier'):
        for k in ['head_dna_file_path','body_dna_file_path']:
            p=o.get(k)
            if p and not any(d['path']==p for d in result['dna_paths']):result['dna_paths'].append({'path':p,'exists':pathlib.Path(bpy.path.abspath(p)).is_file()})
pathlib.Path('D:/UnityProject/MetahumanTemplate/Data/EthanV2/installed_solver_check.json').write_text(json.dumps(result,indent=2,default=str))
print('INSTALLED_SOLVER_CHECK',json.dumps(result,default=str))
