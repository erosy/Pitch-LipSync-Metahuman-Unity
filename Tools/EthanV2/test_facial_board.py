"""Read-only source test: do the screenshot's facial-board controls deform Ethan?

Run in a disposable Blender background process; never save the source file.
"""
import bpy, json, pathlib, numpy as np

root=pathlib.Path('D:/UnityProject/MetahumanTemplate')
face=bpy.data.objects['Ethan_NewMetaHumanCharacter_face_gui']
meshes=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith('Ethan_')]
def coords(o):
    ev=o.evaluated_get(bpy.context.evaluated_depsgraph_get())
    m=ev.to_mesh()
    try:
        a=np.empty(len(m.vertices)*3,dtype=np.float32)
        m.vertices.foreach_get('co',a)
        return a.reshape(-1,3)@np.array(o.matrix_world.to_3x3()).T+np.array(o.matrix_world.translation)
    finally:ev.to_mesh_clear()
def update():
    face.update_tag(refresh={'OBJECT','DATA','TIME'})
    bpy.context.view_layer.update()
    bpy.context.scene.frame_set(bpy.context.scene.frame_current)
    bpy.context.view_layer.update()
base={b.name:b.matrix_basis.copy() for b in face.pose.bones}
update()
neutral={o.name:coords(o) for o in meshes}
tests=[]
for name in ['CTRL_L_eye_blink','CTRL_C_jaw','CTRL_L_mouth_cornerPull','CTRL_L_brow_raiseIn']:
    assert name in face.pose.bones, name
    b=face.pose.bones[name]
    for axis in [0,1,2]:
        for value in [-1.0,1.0]:
            b.matrix_basis=base[name]
            b.location[axis]+=value
            update()
            changes={o.name:float(np.linalg.norm(coords(o)-neutral[o.name],axis=1).max()) for o in meshes}
            tests.append({'control':name,'axis':'xyz'[axis],'delta':value,'evaluated_control_location':list(face.evaluated_get(bpy.context.evaluated_depsgraph_get()).pose.bones[name].location),'mesh_displacement_m':changes,'max_mesh_displacement_m':max(changes.values())})
    b.matrix_basis=base[name]
    update()
result={'source':bpy.data.filepath,'source_saved':False,'tested_controls':4,'samples':len(tests),'max_mesh_displacement_m':max(t['max_mesh_displacement_m'] for t in tests),'tests':tests,'native_driver_status':[]}
for o in bpy.data.objects:
    if 'native' in o.name and o.animation_data:
        for f in o.animation_data.drivers:
            result['native_driver_status'].append({'object':o.name,'expression':f.driver.expression,'valid':f.driver.is_valid,'muted':f.mute})
(root/'Data/EthanV2/facial_board_test.json').write_text(json.dumps(result,indent=2))
print('BOARD_TEST_RESULT', json.dumps({k:v for k,v in result.items() if k!='tests'}))
