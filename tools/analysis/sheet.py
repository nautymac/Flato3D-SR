# sheet.py <folder> <glob> <out.png> [cols]: tiles images (with their names) into one contact sheet
import sys, glob, os
from PIL import Image, ImageDraw
files = sorted(glob.glob(os.path.join(sys.argv[1], sys.argv[2])))
cols = int(sys.argv[4]) if len(sys.argv) > 4 else 6
tw = 320
ims = []
for f in files:
    im = Image.open(f).convert('RGB')
    th = int(im.height * tw / im.width)
    ims.append((os.path.basename(f), im.resize((tw, th))))
th = max(i.height for _, i in ims)
rows = (len(ims) + cols - 1) // cols
sheet = Image.new('RGB', (cols * tw, rows * (th + 14)), (40, 0, 40))
d = ImageDraw.Draw(sheet)
for k, (n, im) in enumerate(ims):
    x, y = (k % cols) * tw, (k // cols) * (th + 14)
    sheet.paste(im, (x, y + 14))
    d.text((x + 2, y + 1), n.split('_')[-1].split('.')[0], fill=(255, 255, 0))
sheet.save(sys.argv[3])
print(len(ims), sheet.size)
