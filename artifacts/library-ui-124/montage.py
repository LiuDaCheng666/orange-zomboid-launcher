from PIL import Image, ImageOps, ImageDraw
from pathlib import Path
base=Path(__file__).resolve().parent
for theme in ('dark','light'):
    files=sorted((base/'visual').glob(theme+'-*.png'))
    rows=(len(files)+2)//3
    canvas=Image.new('RGB',(1500,rows*330),'#dddddd')
    draw=ImageDraw.Draw(canvas)
    for n,p in enumerate(files):
        im=Image.open(p).convert('RGB'); im.thumbnail((490,300))
        x=n%3*500;y=n//3*330
        canvas.paste(im,(x,y+25)); draw.text((x+5,y+5),p.name,fill='black')
    canvas.save(base/(theme+'-visual-sheet.png'))
