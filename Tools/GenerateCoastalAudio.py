"""Original synthesized coastal ambience/foley; only creates missing WAV assets by default."""
from array import array
import argparse
import math
from pathlib import Path
import random
import wave

RATE=22050
ROOT=Path(__file__).resolve().parents[1]/'Assets/Art/Audio/Environment'

def save(name,data,replace):
    path=ROOT/(name+'.wav')
    if path.exists() and not replace:return
    ROOT.mkdir(parents=True,exist_ok=True)
    peak=max(abs(x) for x in data) or 1
    gain=min(1,.72/peak)
    pcm=array('h',(round(max(-1,min(1,x*gain))*32767) for x in data))
    with wave.open(str(path),'wb') as out:
        out.setnchannels(1);out.setsampwidth(2);out.setframerate(RATE);out.writeframes(pcm.tobytes())
    print(name,round(len(data)/RATE,2),'seconds')

def ambient(name,replace):
    duration=24 if name=='Waves' else 16
    size=duration*RATE;overlap=round(.6*RATE);rng=random.Random(name);data=array('f')
    low=mid=0
    for i in range(size+overlap):
        t=i/RATE;n=rng.uniform(-1,1);low+=.012*(n-low);mid+=.19*(n-mid)
        if name=='Waves':
            wash=(.5+.5*math.sin(2*math.pi*t/8-.5))**2
            small=(.5+.5*math.sin(2*math.pi*t/3+.7))**3
            value=(low*3.2+mid*.50+n*.07)*(.12+.78*wash+.10*small)
        elif name=='Wind':
            gust=.48+.28*math.sin(2*math.pi*t/16)+.16*math.sin(2*math.pi*t/4+.5)
            value=(low*2.1+mid*.24)*gust
        else:
            value=(n*.16+mid*.30+low*.55)*(.9+.1*math.sin(2*math.pi*t/8))
        data.append(value)
    # Overlap-add makes the loop boundary follow adjacent source samples instead of a hard cut.
    for i in range(overlap):
        a=.5-.5*math.cos(math.pi*i/(overlap-1))
        data[i]=data[size+i]*(1-a)+data[i]*a
    save(name,data[:size],replace)

def effect(name,replace):
    duration={'SeagullCall':1.95,'WingFlap':.78,'StepGrass':.19,'StepSand':.24,'StepWood':.18}[name]
    size=round(duration*RATE);rng=random.Random(name);data=array('f');low=mid=phase=0
    for i in range(size):
        t=i/RATE;n=rng.uniform(-1,1);low+=.10*(n-low);mid+=.45*(n-mid)
        if name=='SeagullCall':
            value=0
            for start,length,pitch in [(.07,.44,1200),(.61,.54,1080),(1.22,.59,1010)]:
                q=(t-start)/length
                if 0<=q<1:
                    frequency=pitch+330*math.sin(math.pi*q)-390*q+30*math.sin(t*87)
                    phase+=2*math.pi*frequency/RATE
                    env=math.sin(math.pi*q)**.65
                    value=(math.sin(phase)*.24+math.sin(phase*2)*.10+math.sin(phase*3)*.035+mid*.10)*env*(.84+.16*math.sin(t*73))
        elif name=='WingFlap':
            value=0
            for start in [0,.25,.49]:
                q=(t-start)/.23
                if 0<=q<1:value+=(low*1.5+mid*.25)*math.sin(math.pi*q)**2
        else:
            env=min(1,t/.006)*math.exp(-t*(22 if name=='StepWood' else 17))*min(1,(duration-t)/.035)
            if name=='StepWood':value=(.23*math.sin(2*math.pi*178*t)+.15*math.sin(2*math.pi*510*t)+mid*.24)*env
            elif name=='StepSand':value=(low*.7+mid*.55+n*.18)*env*(.8+.2*math.sin(t*133))
            else:value=(low*1.1+mid*.34+.10*math.sin(2*math.pi*95*t))*env
        data.append(value)
    save(name,data,replace)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--replace',action='store_true');args=parser.parse_args()
    for name in ['Waves','Wind','Rain']:ambient(name,args.replace)
    for name in ['SeagullCall','WingFlap','StepGrass','StepSand','StepWood']:effect(name,args.replace)
