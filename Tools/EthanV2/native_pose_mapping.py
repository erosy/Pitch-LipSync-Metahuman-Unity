"""Project's 73 ordered names mapped to original MetaHuman GUI controls.

Single facial motions use corresponding GUI controls; speech and emotions use
Polyhammer's installed face-board pose library. The original native solver
evaluates the full combined pose, including its nonlinear correctives.
"""
import json
from pathlib import Path

POSES=Path('C:/Users/ina17/AppData/Roaming/Blender Foundation/Blender/5.2/extensions/api_portal_polyhammer_com_001/character_dna/resources/poses/face')

def make_mapping(gui_names):
    valid=set(gui_names);p={};provenance={}
    def put(name,*items):
        p[name]={}
        for bone,axis,value in items:p[name].setdefault(bone,{})[axis]=value
        provenance[name]='Corresponding MetaHuman GUI controls'
    def y(bone,v=1):return ('CTRL_'+bone,'y',v)
    def x(bone,v=1):return ('CTRL_'+bone,'x',v)
    def load(name,category,folder):
        data=json.loads((POSES/category/folder/'pose.json').read_text())['face_board']
        p[name]={}
        for bone,value in data.items():
            for axis,i in [('x',0),('y',1),('z',2)]:
                if bone+'.t'+axis in valid and abs(value['location'][i])>1e-6:
                    p[name].setdefault(bone,{})[axis]=float(value['location'][i])
        provenance[name]='Polyhammer face-board pose library: '+category+'/'+folder
    for side,word,sign in [('L','Left',1),('R','Right',-1)]:
        put('eyeBlink'+word,y(side+'_eye_blink'))
        put('eyeWide'+word,y(side+'_eye_blink',-1))
        put('eyeSquint'+word,y(side+'_eye_lidPress'),y(side+'_eye_squintInner',.5))
        for direction,axis,value in [('Up','y',1),('Down','y',-1),('In','x',-sign),('Out','x',sign)]:
            put('eyeLook'+direction+word,('CTRL_'+side+'_eye',axis,value))
        put('browDown'+word,y(side+'_brow_down'))
        put('browOuterUp'+word,y(side+'_brow_raiseOut'))
        put('cheekSquint'+word,y(side+'_eye_cheekRaise'))
        put('noseSneer'+word,y(side+'_nose_wrinkleUpper'),y(side+'_nose',.5))
        for stem,control in [('Smile','cornerPull'),('Frown','cornerDepress'),('Dimple','dimple'),('Stretch','stretch'),('LowerDown','lowerLipDepress'),('UpperUp','upperLipRaise')]:
            put('mouth'+stem+word,y(side+'_mouth_'+control))
        put('mouthPress'+word,y(side+'_mouth_pressU'),y(side+'_mouth_pressD'))
    put('browInnerUp',y('L_brow_raiseIn'),y('R_brow_raiseIn'))
    put('cheekPuff',y('L_mouth_suckBlow'),y('R_mouth_suckBlow'))
    put('jawOpen',y('C_jaw'))
    put('jawForward',y('C_jaw_fwdBack'))
    put('jawLeft',x('C_jaw'))
    put('jawRight',x('C_jaw',-1))
    # LipsTogether activates with jaw opening. The baker subtracts jawOpen from
    # this posed result to produce the independent additive ARKit mouthClose.
    put('mouthClose',y('C_jaw'),*[y(s+'_mouth_lipsTogether'+part) for s in ['L','R'] for part in ['U','D']])
    load('mouthFunnel','scan_reference','Funneler')
    load('mouthPucker','scan_reference','Pucker')
    put('mouthLeft',x('C_mouth'))
    put('mouthRight',x('C_mouth',-1))
    put('mouthRollLower',y('L_mouth_lipsRollD'),y('R_mouth_lipsRollD'))
    put('mouthRollUpper',y('L_mouth_lipsRollU'),y('R_mouth_lipsRollU'))
    put('mouthShrugLower',y('L_jaw_ChinRaiseD'),y('R_jaw_ChinRaiseD'))
    put('mouthShrugUpper',y('L_jaw_ChinRaiseU'),y('R_jaw_ChinRaiseU'))
    put('tongueOut',y('C_tongue_inOut'),y('C_jaw',.25))
    put('viseme_sil')
    for suffix,folder in {'PP':'23_MBP','FF':'20_FV','TH':'25_Tongue-Th','DD':'01_TDS-Ah','kk':'26_Tongue-KGY','CH':'14_ChJjSh','SS':'23_Tongue_LNTDS','nn':'24_Tongue-LNTDS','RR':'17_Rr','aa':'08_Ah','E':'03_Ee','I':'04_Ih','O':'09_Oh','U':'12_Oo'}.items():
        load('viseme_'+suffix,'visemes',folder)
    # The Tongue library entries open the jaw fully to expose tongue anatomy.
    # Use speech-size openings while retaining the original tongue controls;
    # let RigLogic solve its correctives for the resulting complete pose.
    for suffix,opening in {'TH':.22,'kk':.25,'SS':.08,'nn':.12}.items():
        p['viseme_'+suffix].setdefault('CTRL_C_jaw',{})['y']=opening
        provenance['viseme_'+suffix]+='; jaw opening adapted for speech: '+str(opening)
    for suffix,folder in {'joy':'Joy-03_Joy','sadness':'Sadness-03_Sadness','anger':'Anger-03_Anger','surprise':'Surprise-03_Surprise','fear':'Fear-03_Fear','disgust':'Disgust-03_Disgust'}.items():
        load('emo_'+suffix,'emotions',folder)
    for name,controls in p.items():
        for bone,axes in controls.items():
            for axis in axes:assert bone+'.t'+axis in valid,(name,bone,axis)
    return p,provenance
