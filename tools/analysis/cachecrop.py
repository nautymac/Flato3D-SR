# cachecrop.py <dir> <frame> <out.png> <draws...>: crops the low-res cache region of step images, flips, tiles 2 per row
import sys, os
from PIL import Image, ImageDraw, ImageChops
d, fr, out = sys.argv[1], sys.argv[2], sys.argv[3]
ids = [int(x) for x in sys.argv[4:]]
tiles = []
prev = None
for i in ids:
    im = Image.open(os.path.join(d, f'OGL3D_step{fr}_{i:03d}.bmp')).convert('RGB')
    W, H = im.size
    c = im.crop((0, H - int(H * 0.235), int(W * 0.41), H)).transpose(Image.FLIP_TOP_BOTTOM)
    tiles.append((i, c))
w, h = tiles[0][1].size
sc = 3
cols = 2
sheet = Image.new('RGB', (cols * w * sc, ((len(tiles) + cols - 1) // cols) * (h * sc + 14)), (40, 0, 40))
dr = ImageDraw.Draw(sheet)
for k, (i, c) in enumerate(tiles):
    x, y = (k % cols) * w * sc, (k // cols) * (h * sc + 14)
    sheet.paste(c.resize((w * sc, h * sc), Image.NEAREST), (x, y + 14))
    dr.text((x + 2, y + 1), str(i), fill=(255, 255, 0))
sheet.save(out)
print(sheet.size)
