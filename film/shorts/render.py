"""Render portrait graphics, composite native demo footage, and mix offline SAPI narration."""
import argparse, concurrent.futures, hashlib, html, json, math, os, shutil, subprocess, wave
from pathlib import Path

HERE=Path(__file__).resolve().parent
FILM=HERE.parent
WORK=Path(os.environ.get('DOCKBOT_MEDIA_WORK', 'E:/DockBot-Media/shorts-work' if Path('E:/DockBot-Media').exists() else str(FILM.parent/'artifacts/shorts-work')))
EXPORT=HERE/'exports'
MANIFEST=json.loads((HERE/'manifest.json').read_text(encoding='utf-8'))
NATIVE=FILM/'public/native-demo.mp4'
MUSIC=FILM/'public/score-master.wav'

def run(args,log):
    with open(log,'w',encoding='utf-8') as out:
        p=subprocess.run([str(a) for a in args],cwd=FILM,stdout=out,stderr=subprocess.STDOUT)
    if p.returncode:raise RuntimeError(f'{log}: '+Path(log).read_text(encoding='utf-8',errors='replace')[-2400:])

def duration(path):
    with wave.open(str(path)) as f:return f.getnframes()/f.getframerate()

def stamp(seconds,ass=False):
    if ass:
        n=round(seconds*100);return f'{n//360000}:{n//6000%60:02}:{n//100%60:02}.{n%100:02}'
    n=round(seconds*1000);return f'{n//3600000:02}:{n//60000%60:02}:{n//1000%60:02},{n%1000:03}'

def wrapped(text):
    lines=[];line='';size=0
    for c in text:
        weight=1 if ord(c)>255 else .55
        if size+weight>17:lines.append(line);line='';size=0
        line+=c;size+=weight
    if line:lines.append(line)
    return r'\N'.join(lines)

def graphics(film):
    work=WORK/film['id'];work.mkdir(parents=True,exist_ok=True)
    out=work/'graphics.mp4'
    signature=hashlib.sha256((HERE/'Portrait.tsx').read_bytes()+(FILM/'src/components.tsx').read_bytes()+(FILM/'src/theme.ts').read_bytes()+json.dumps([{k:v for k,v in s.items() if k!='voice'} for s in film['scenes']],ensure_ascii=False).encode()).hexdigest()
    stamp_file=work/'graphics.sha256'
    if not out.exists() or not stamp_file.exists() or stamp_file.read_text()!=signature:
        pending=work/'graphics-pending.mp4'
        run(['node',FILM/'node_modules/@remotion/cli/remotion-cli.js','render','src/index.tsx',film['id'].replace('_','-')+'-Graphics',pending,'--codec','h264','--crf','18','--x264-preset','ultrafast','--concurrency','2','--log','warn'],work/'graphics.log')
        pending.replace(out);stamp_file.write_text(signature)
    return out

def compose(film):
    name=film['id'];work=WORK/name;out=EXPORT/name;out.mkdir(parents=True,exist_ok=True)
    graphics_path=work/'graphics.mp4';args=['ffmpeg','-hide_banner','-y','-threads','2','-i',graphics_path]
    inputs=[];clips=[]
    for scene in film['scenes']:
        if not scene['shot']:continue
        shot=MANIFEST['shots'][scene['shot']];x,y,w,h=shot['crop'];bx,by,bw,bh=shot['box']
        clip_key=hashlib.sha256(json.dumps([shot,scene['start'],scene['duration'],NATIVE.stat().st_mtime_ns]).encode()).hexdigest()[:10]
        clip=work/f"clip-{scene['index']}-{clip_key}.mp4"
        if not clip.exists():
            # Seek each source shot separately: no large frame queues when story time jumps.
            crop=f'crop={w}:{h}:{x}:{y}'
            if shot.get('pan'):crop=f"crop={w}:{h}:x='{x}+{shot['pan']}*(0.5-0.5*cos(PI*min(t/{scene['duration']},1)))':y={y}"
            run(['ffmpeg','-hide_banner','-y','-threads','2','-ss',scene['start'],'-i',NATIVE,'-t',scene['duration'],'-an','-vf',crop+f',scale={bw}:{bh}:flags=lanczos,setsar=1','-r','30','-c:v','libx264','-preset','veryfast','-crf','16','-threads','2',clip],work/f"clip-{scene['index']}.log")
        args+=['-threads','1','-i',clip];clips.append((len(clips)+1,scene,shot))
    end_index=len(clips)+1;args+=['-i',WORK/'end-ready.mp4']
    music_index=len(clips)+2;args+=['-i',MUSIC]
    voices=[]
    for scene in film['scenes']:
        if not scene['voice']:continue
        path=work/f"voice-{scene['index']}.wav";raw=duration(path);speed=max(1,raw/max(.2,scene['duration']-.16))
        if speed>1.4:raise RuntimeError(f"Narration too long: {name}/{scene['index']} {raw:.2f}s")
        index=music_index+1+len(voices);args+=['-i',path];voices.append((index,scene,raw,speed))
    filters=[];previous='0:v'
    for n,(index,scene,shot) in enumerate(clips):
        at=scene['at'];end=at+scene['duration'];x,y,w,h=shot['box'];label=f'clip{n}';output=f'layer{n}'
        filters.append(f'[{index}:v]setpts=PTS-STARTPTS+{at}/TB[{label}]')
        filters.append(f'[{previous}][{label}]overlay=x={x}:y={y}:enable=\'between(t,{at},{end-.001})\':eof_action=pass:repeatlast=0[{output}]');previous=output
    end_duration=film['scenes'][-1]['duration'];end_at=film['duration']-end_duration
    filters.append(f'[{end_index}:v]trim=duration={end_duration},setpts=PTS-STARTPTS+{end_at}/TB[ending]')
    filters.append(f'[{previous}][ending]overlay=enable=\'gte(t,{end_at})\':eof_action=pass:repeatlast=0[complete]');previous='complete'
    ass_header='''[Script Info]
ScriptType: v4.00+
PlayResX: 1080
PlayResY: 1920
WrapStyle: 0
[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,Microsoft YaHei,46,&H00F5F5F5,&H00FFFFFF,&HC0000000,&H70000000,0,0,0,0,100,100,0,0,1,2,1,2,90,140,220,1
[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
'''
    ass=[];srt=[];voice_mix=[]
    for n,(index,scene,raw,speed) in enumerate(voices):
        start=scene['at']+.06;end=min(scene['at']+scene['duration']-.04,start+raw/speed);delay=round(start*1000)
        label=f'voice{n}';filters.append(f'[{index}:a]atempo={speed:.5f},loudnorm=I=-16:TP=-1:LRA=8,aresample=48000,adelay={delay}:all=1[{label}]');voice_mix.append(label)
        ass.append(f"Dialogue: 0,{stamp(start,True)},{stamp(end,True)},Default,,0,0,0,,{wrapped(scene['voice'])}")
        srt.append(f"{n+1}\n{stamp(start)} --> {stamp(end)}\n{scene['voice']}\n")
    (work/'captions.ass').write_text(ass_header+'\n'.join(ass)+'\n',encoding='utf-8-sig')
    # Relative subtitle filename avoids Windows drive-colon filter escaping.
    ass_file=str((work/'captions.ass').resolve()).replace('\\','/').replace(':',r'\:')
    filters.append(f"[{previous}]subtitles=filename='{ass_file}',format=yuv420p[video]")
    filters.append(f'[{music_index}:a]atrim=0:{film["duration"]},asetpts=PTS-STARTPTS,volume={.18 if voices else .68},afade=t=out:st={film["duration"]-.65}:d=0.65[music]')
    labels=''.join(f'[{x}]' for x in ['music']+voice_mix)
    filters.append(f'{labels}amix=inputs={len(voice_mix)+1}:normalize=0,alimiter=limit=.94:level=0,aresample=48000[audio]')
    script=work/'compose.ffgraph';script.write_text(';\n'.join(filters),encoding='utf-8')
    destination=out/(name+'.mp4')
    args+=['-filter_complex_threads','2','-/filter_complex',script,'-map','[video]','-map','[audio]','-t',film['duration'],'-c:v','libx264','-preset','veryfast','-crf','18','-threads','4','-c:a','aac','-b:a','192k','-movflags','+faststart',destination]
    run(args,work/'compose.log')
    (out/(name+'.srt')).write_text('\n'.join(srt),encoding='utf-8-sig')
    (out/'旁白.txt').write_text('\n'.join(f"{s['at']:05.2f}s  {s['voice']}" for s in film['scenes'] if s['voice']) or '纯音乐视觉版，无旁白。',encoding='utf-8')
    metadata={k:film[k] for k in ('id','title','audience','tags','duration','baseline','disclosure')}
    metadata['description']='DockBot：Windows 桌面助手，原生 Dock、本地意图模型和自然语言整理。源码与下载：https://github.com/nanhudev/dockbot'
    metadata['voice']='Windows SAPI · Microsoft Huihui Desktop' if voices else '无旁白'
    metadata['music']='ClearSpace · 原创配乐'
    (out/'发布信息.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
    # Choose a stable frame after the hook settles, keeping cover consistent with the film.
    cover_time=2.5 if film['id']!='V7_10Second_Hook' else .8
    run(['ffmpeg','-hide_banner','-y','-ss',cover_time,'-i',destination,'-frames:v','1',out/'cover.png'],work/'cover.log')
    probe=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_streams','-show_format','-of','json',str(destination)]))
    video=next(s for s in probe['streams'] if s['codec_type']=='video');audio=next(s for s in probe['streams'] if s['codec_type']=='audio')
    assert (video['width'],video['height'],video['r_frame_rate'],video['codec_name'],audio['codec_name'])==(1080,1920,'30/1','h264','aac')
    assert abs(float(probe['format']['duration'])-film['duration'])<.12
    run(['ffmpeg','-v','error','-i',destination,'-f','null','-'],work/'decode.log')
    (out/'验证.json').write_text(json.dumps(dict(width=1080,height=1920,fps=30,duration=probe['format']['duration'],video='h264',audio='aac',fullDecodePassed=True,sha256=hashlib.sha256(destination.read_bytes()).hexdigest()),indent=2),encoding='utf-8')
    print(f'COMPLETE {name}: {film["duration"]}s',flush=True)
    return destination

def index():
    cards=[]
    for film in MANIFEST['films']:
        name=film['id'];path=EXPORT/name/(name+'.mp4')
        if path.exists():cards.append(f'<article><h2>{html.escape(film["title"])}</h2><video controls preload="none" poster="{name}/cover.png" src="{name}/{name}.mp4"></video><p>{film["duration"]} 秒 · <a href="{name}/{name}.mp4" download>下载</a></p></article>')
    (EXPORT/'index.html').write_text('<!doctype html><meta charset="utf-8"><title>DockBot 短片</title><style>body{background:#0a1018;color:#edf6f5;font:16px system-ui;margin:40px}main{display:flex;flex-wrap:wrap;gap:24px}article{width:320px}h2{font-size:19px;min-height:50px}video{width:100%;border-radius:24px}a{color:#83dfcc}</style><h1>DockBot 短片</h1><main>'+''.join(cards)+'</main>',encoding='utf-8')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--ids',nargs='*');parser.add_argument('--graphics-only',action='store_true');parser.add_argument('--compose-only',action='store_true');args=parser.parse_args()
    films=[f for f in MANIFEST['films'] if not args.ids or f['id'] in args.ids]
    EXPORT.mkdir(parents=True,exist_ok=True)
    if not args.compose_only:
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            for f in pool.map(graphics,films):print('GRAPHICS '+str(f),flush=True)
    if not args.graphics_only:
        end_signature=hashlib.sha256((HERE/'Portrait.tsx').read_bytes()+(FILM/'src/components.tsx').read_bytes()+(FILM/'src/theme.ts').read_bytes()).hexdigest();end_stamp=WORK/'ending.sha256'
        if not (WORK/'end-ready.mp4').exists() or not end_stamp.exists() or end_stamp.read_text()!=end_signature:
            ref=next(f for f in MANIFEST['films'] if f['id']=='V2_Technical_17KB');start=round(ref['scenes'][-1]['at']*30);end=round(ref['duration']*30)-1
            run(['node',FILM/'node_modules/@remotion/cli/remotion-cli.js','render','src/index.tsx','V2-Technical-17KB-Graphics',WORK/'end-pending.mp4','--frames',f'{start}-{end}','--codec','h264','--crf','18','--x264-preset','ultrafast','--concurrency','2','--log','warn'],WORK/'ending.log')
            (WORK/'end-pending.mp4').replace(WORK/'end-ready.mp4');end_stamp.write_text(end_signature)
        for f in films:
            if any(s['voice'] and not (WORK/f['id']/f"voice-{s['index']}.wav").exists() for s in f['scenes']):
                run(['powershell','-NoProfile','-ExecutionPolicy','Bypass','-File',HERE/'voice.ps1','-Manifest',HERE/'manifest.json','-Destination',WORK,'-FilmId',f['id']],WORK/f['id']/'voice.log')
        for film in films:compose(film)
        index()
