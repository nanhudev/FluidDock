"""DockBot / Clear Space: an original 120 BPM instrumental, no sampled recordings."""
from pathlib import Path
import wave
import numpy as np

RATE=48000
SECONDS=70
ROOT=Path(__file__).resolve().parents[1]
rng=np.random.default_rng(728)
audio=np.zeros((RATE*SECONDS,2),dtype=np.float64)

def freq(midi): return 440*2**((midi-69)/12)
def put(signal,start,gain=1,pan=0):
    offset=max(0,int(start*RATE)); count=min(len(signal),len(audio)-offset)
    if count<=0:return
    audio[offset:offset+count,0]+=signal[:count]*gain*np.sqrt((1-pan)/2)
    audio[offset:offset+count,1]+=signal[:count]*gain*np.sqrt((1+pan)/2)
def clock(duration):return np.arange(int(duration*RATE))/RATE

for bar in range(35):
    start=bar*2
    root,chord=[(38,[62,65,69,72,76]),(34,[58,62,65,69,72]),(29,[57,60,64,67,72]),(36,[60,64,67,74,76])][bar%4]
    t=clock(2.9); envelope=np.minimum(1,t/.13)*np.exp(-t/1.5)*np.minimum(1,(2.9-t)/.45)
    for i,note in enumerate(chord):
        f=freq(note);pad=(np.sin(2*np.pi*f*t)+.3*np.sin(2*np.pi*(f*1.002)*t)+.12*np.sin(4*np.pi*f*t))
        put(pad*envelope,start,.014,(-1 if i%2 else 1)*.45)
    if 2<=bar<=32:
        for beat in [0,1.5,2,3.5]:
            t=clock(.31);f=freq(root);env=(1-np.exp(-t*100))*np.exp(-t*13)
            put((np.sin(2*np.pi*f*t)+.23*np.sin(4*np.pi*f*t))*env,start+beat*.5,.13)
        for step in range(8):
            note=chord[[0,2,4,1,2,3,1,4][step]]+(12 if bar%8==7 and step>3 else 0)
            t=clock(.7);f=freq(note);env=(1-np.exp(-t*650))*np.exp(-t*8)
            pluck=(np.sin(2*np.pi*f*t+1.1*np.sin(4*np.pi*f*t)*np.exp(-t*14))+.14*np.sin(6*np.pi*f*t))*env
            moment=start+step*.25+(0.012 if step%2 else 0);gain=.035 if step%2 else .055;pan=np.sin(step*1.3)*.5
            put(pluck,moment,gain,pan);put(pluck,moment+.375,gain*.24,-pan);put(pluck,moment+.75,gain*.09,pan)
    if 2<=bar<=32:
        for beat in range(4):
            t=clock(.24);phase=2*np.pi*(43*t+7.7*(1-np.exp(-t*19)))
            kick=np.sin(phase)*np.exp(-t*19)+rng.normal(0,.025,len(t))*np.exp(-t*200)
            put(kick,start+beat*.5,.32)
            if beat%2:
                t=clock(.18);noise=rng.normal(0,1,len(t));noise=np.r_[0,np.diff(noise)]*.5
                clap=noise*np.exp(-t*32)*(1+.35*np.sin(2*np.pi*70*t))+np.sin(2*np.pi*185*t)*np.exp(-t*45)*.18
                put(clap,start+beat*.5,.055,.08)
        for step in range(8):
            t=clock(.08 if step%2==0 else .14);noise=rng.normal(0,1,len(t));noise=np.r_[0,np.diff(noise)]
            put(noise*np.exp(-t*(90 if step%2==0 else 48)),start+step*.25,.012 if step%2==0 else .018,(-1 if step%2 else 1)*.35)

for hit in [0,2,6,13,17,20,24,27,30,32,35,38,41.5,44,49,52,55,59.5,62,66,68]:
    t=clock(.17);noise=rng.normal(0,1,len(t));ramp=np.sin(np.pi*np.minimum(1,t/.17))**2
    put(noise*ramp*.025,max(0,hit-.17),.5,-.2)
    t=clock(.15);put(np.sin(2*np.pi*(95*t-22*t*t))*np.exp(-t*34),hit,.14)

# Quiet landing cues follow the native actor's contact frames.
for contact in [19.42,22.62,29.42,37.42,40.62,61.92]:
    t=clock(.095)
    pop=(np.sin(2*np.pi*(780*t-1800*t*t))*.48+np.sin(2*np.pi*132*t)*.7)*np.exp(-t*54)
    put(pop,contact,.075)

t=np.arange(len(audio))/RATE
fade=np.minimum(1,t/.08)*np.minimum(1,np.maximum(0,(SECONDS-t)/1.2))
audio=np.tanh(audio*1.8)*fade[:,None]
audio=audio/max(1,np.max(np.abs(audio))/.92)
out=ROOT/'public'/'clear-space.wav';out.parent.mkdir(exist_ok=True)
with wave.open(str(out),'wb') as f:
    f.setnchannels(2);f.setsampwidth(2);f.setframerate(RATE);f.writeframes((audio*32767).astype('<i2').tobytes())
print(out)
