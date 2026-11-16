import cv2
from pathlib import Path
source=Path(r'C:\Users\ina17\Videos\Captures\MetahumanTemplate - CharacterTesting - Windows, Mac, Linux - Unity 6.5 (6000.5.9f1)_ _DX12_ 2026-11-16 12-17-33.mp4')
out=Path('Data/EthanV2/CollarReview');out.mkdir(exist_ok=True,parents=True)
cap=cv2.VideoCapture(str(source));count=cap.get(cv2.CAP_PROP_FRAME_COUNT);fps=cap.get(cv2.CAP_PROP_FPS);print({'seconds':count/fps,'fps':fps})
for i,f in enumerate([.03,.25,.5,.75,.95]):
 cap.set(cv2.CAP_PROP_POS_FRAMES,int(count*f));ok,frame=cap.read()
 if ok:cv2.imwrite(str(out/f'frame_{i}.png'),frame)
cap.release()
cap=cv2.VideoCapture(str(source));items=[];index=0
while True:
 ok,frame=cap.read()
 if not ok:break
 if index%15==0:
  crop=frame[450:980,200:510];crop=cv2.resize(crop,(186,318));cv2.putText(crop,str(index),(5,20),cv2.FONT_HERSHEY_SIMPLEX,.5,(0,0,255),1);items.append(crop)
 index+=1
cap.release()
import numpy as np
while len(items)%8:items.append(np.zeros_like(items[0]))
cv2.imwrite(str(out/'contact.png'),np.concatenate([np.concatenate(items[i:i+8],axis=1) for i in range(0,len(items),8)],axis=0));print('decoded',index)
