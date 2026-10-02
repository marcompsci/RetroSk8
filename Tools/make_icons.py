# Generates the original Retro Sk8 app icon and launch screen. Run from Assets/RetroSk8/Art/AppIcon: python3 ../../../../Tools/make_icons.py
from PIL import Image, ImageDraw
INK=(18,19,23); CORAL=(255,89,77); TAPE=(242,194,48); CREAM=(242,232,209); TEAL=(31,199,181)

def bands(img, w, h, offset=0.0):
    d=ImageDraw.Draw(img)
    import math
    # diagonal tape bands (the menu motif)
    for (col, c0, thick) in [(CORAL, 0.52, 0.17), (TAPE, 0.74, 0.06)]:
        cy=h*(c0+offset); t=h*thick; slope=math.tan(math.radians(14))
        poly=[(-w, cy - t/2 + slope*w), (2*w, cy - t/2 - slope*2*w), (2*w, cy + t/2 - slope*2*w), (-w, cy + t/2 + slope*w)]
        d.polygon(poly, fill=col)

def scanlines(img, step, alpha):
    over=Image.new('RGBA', img.size, (0,0,0,0)); d=ImageDraw.Draw(over)
    for y in range(0, img.size[1], step): d.rectangle([0,y,img.size[0],y+step//3], fill=(0,0,0,alpha))
    img.alpha_composite(over)

def deck(size, angle):
    W,H=size
    layer=Image.new('RGBA',(W,H),(0,0,0,0)); d=ImageDraw.Draw(layer)
    bw, bh = W*0.78, H*0.2
    x0,y0=(W-bw)/2,(H-bh)/2
    d.rounded_rectangle([x0,y0,x0+bw,y0+bh], radius=bh/2, fill=CREAM, outline=INK, width=int(W*0.018))
    # grip stripe and bolts
    d.rounded_rectangle([x0+bw*0.08,y0+bh*0.38,x0+bw*0.92,y0+bh*0.62], radius=bh*0.12, fill=TEAL)
    for fx in (0.22,0.78):
        for fy in (0.2,0.8):
            cx=x0+bw*fx; cy=y0+bh*fy; r=W*0.012
            d.ellipse([cx-r,cy-r,cx+r,cy+r], fill=INK)
    # wheels
    for fx in (0.2,0.8):
        cx=x0+bw*fx; cy=y0+bh*1.18; r=bh*0.24
        d.ellipse([cx-r,cy-r,cx+r,cy+r], fill=TAPE, outline=INK, width=int(W*0.014))
    return layer.rotate(angle, resample=Image.BICUBIC)

# App icon: 1024 square, no transparency (App Store rule).
S=1024
icon=Image.new('RGBA',(S,S),INK+(255,))
bands(icon,S,S)
icon.alpha_composite(deck((S,S), 18))
scanlines(icon, 12, 40)
icon.convert('RGB').save('RetroSk8_AppIcon.png')

# Block-letter title for the launch screen (hand-made 5x7 glyphs).
G={
'R':["1111.","1...1","1...1","1111.","1.1..","1..1.","1...1"],
'E':["11111","1....","1....","1111.","1....","1....","11111"],
'T':["11111","..1..","..1..","..1..","..1..","..1..","..1.."],
'O':[".111.","1...1","1...1","1...1","1...1","1...1",".111."],
'S':[".1111","1....","1....",".111.","....1","....1","1111."],
'K':["1...1","1..1.","1.1..","11...","1.1..","1..1.","1...1"],
'8':[".111.","1...1","1...1",".111.","1...1","1...1",".111."],
' ':["....."]*7}
def title(img, text, x, y, px, col, shadow):
    d=ImageDraw.Draw(img)
    for dx,dy,c in ((px*0.6,px*0.6,shadow),(0,0,col)):
        cx=x
        for ch in text:
            for r,row in enumerate(G[ch]):
                for c_,v in enumerate(row):
                    if v=='1': d.rectangle([cx+c_*px+dx, y+r*px+dy, cx+(c_+1)*px-1+dx, y+(r+1)*px-1+dy], fill=c)
            cx+=6*px

W,H=2732,2048  # covers every iPhone/iPad landscape aspect when centred
ls=Image.new('RGBA',(W,H),INK+(255,))
bands(ls,W,H,0.05)
px=34; text="RETRO SK8"; tw=len(text)*6*px-px
title(ls,text,(W-tw)//2,int(H*0.36),px,TAPE,INK)
ls.alpha_composite(deck((900,900),-12),(W//2-450,int(H*0.47)))
scanlines(ls, 10, 35)
ls.convert('RGB').save('RetroSk8_LaunchScreen.png')
print("ok")
