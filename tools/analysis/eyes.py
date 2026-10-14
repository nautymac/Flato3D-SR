# eyes.py <OGL3D_frame.bmp> <out_prefix>: writes <prefix>_ana.png (red-cyan anaglyph) and <prefix>_sbs.png (small side by side)
import sys
from PIL import Image, ImageChops
im=Image.open(sys.argv[1]).convert('RGB'); W,H=im.size
print('bmp',W,H)
if W>=H*3: L=im.crop((0,0,W//2,H)); R=im.crop((W//2,0,W,H))
else: L=im.crop((0,0,W,H//2)); R=im.crop((0,H//2,W,H))
w,h=L.size
ana=Image.merge('RGB',(L.split()[0],R.split()[1],R.split()[2])).resize((w//3,h//3))
ana.save(sys.argv[2]+'_ana.png')
sbs=Image.new('RGB',(w//4*2,h//4)); sbs.paste(L.resize((w//4,h//4)),(0,0)); sbs.paste(R.resize((w//4,h//4)),(w//4,0)); sbs.save(sys.argv[2]+'_sbs.png')
diff=ImageChops.difference(L,R).convert('L').resize((w//6,h//6)); diff.save(sys.argv[2]+'_diff.png')
