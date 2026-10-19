"""Render the project's own geometric DockBot mark (optional build asset)."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1] / 'assets'
root.mkdir(exist_ok=True)
image = Image.new('RGBA', (256, 256))
d = ImageDraw.Draw(image)
d.rounded_rectangle((14, 14, 242, 242), 66, fill='#17222d')
d.rounded_rectangle((43, 61, 213, 205), 52, fill='#83cec6')
d.ellipse((84, 113, 104, 146), fill='#153542')
d.ellipse((152, 113, 172, 146), fill='#153542')
d.rounded_rectangle((65, 79, 139, 85), 3, fill='#bbe8df')
image.save(root / 'buddy.ico', sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
