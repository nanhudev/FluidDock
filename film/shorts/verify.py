"""Extract encoded review frames and a contact sheet; never touches user files."""
import argparse,json,subprocess
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
HERE=Path(__file__).resolve().parent
parser=argparse.ArgumentParser();parser.add_argument('--ids',nargs='*');args=parser.parse_args()
manifest=json.loads((HERE/'manifest.json').read_text(encoding='utf-8'))
review=Path('E:/DockBot-Media/shorts-review');review.mkdir(parents=True,exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
for film in manifest['films']:
    if args.ids and film['id'] not in args.ids:continue
    source=HERE/'exports'/film['id']/(film['id']+'.mp4')
    if not source.exists():continue
    frames=[]
    for scene in film['scenes']:
        at=scene['at']+min(1,scene['duration']*.55);target=review/f"{film['id']}-{scene['index']}.png"
        subprocess.run(['ffmpeg','-v','error','-y','-ss',str(at),'-i',str(source),'-frames:v','1','-vf','scale=324:576',str(target)],check=True)
        image=Image.open(target).convert('RGB');tile=Image.new('RGB',(324,610),'#151e29');tile.paste(image,(0,28));ImageDraw.Draw(tile).text((8,3),f"{at:.1f}s / {scene['title'].replace(chr(10),' ')}",font=font,fill='#dcece9');frames.append(tile)
    sheet=Image.new('RGB',(324*4,610*((len(frames)+3)//4)),'#0a1018')
    for i,frame in enumerate(frames):sheet.paste(frame,((i%4)*324,(i//4)*610))
    sheet.save(review/(film['id']+'-contact.jpg'),quality=92)
    result=subprocess.run(['ffmpeg','-hide_banner','-i',str(source),'-af','volumedetect','-vf','blackdetect=d=0.2:pix_th=0.02:pic_th=0.98','-f','null','-'],capture_output=True,text=True,encoding='utf-8',errors='replace')
    (review/(film['id']+'-quality.log')).write_text(result.stderr,encoding='utf-8')
    if result.returncode:raise RuntimeError(result.stderr[-1000:])
    print(film['id']+' reviewed frames ready',flush=True)
