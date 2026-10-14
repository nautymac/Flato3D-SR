# dispmap.py <frame.bmp> <out.png>: dense disparity map (right - left, full-res px) of an OGL3D SBS dump,
# drawn as numbers over the dimmed left eye; only textured blocks are measured
import sys
import numpy as np
from PIL import Image, ImageDraw
img = Image.open(sys.argv[1]).convert('RGB')
im = np.asarray(img.convert('L'), dtype=np.float32)
H, W2 = im.shape; W = W2 // 2
L, R = im[:, :W], im[:, W:]
bs, maxd = 24, 48
out = img.crop((0, 0, W, H)).point(lambda v: v // 2)
d = ImageDraw.Draw(out)
for y0 in range(0, H - bs, bs):
    for x0 in range(0, W - bs, bs):
        a = L[y0:y0+bs, x0:x0+bs]
        if a.std() < 8: continue
        best = None; second = None
        for dd in range(-maxd, maxd + 1):
            if x0 + dd < 0 or x0 + dd + bs > W: continue
            s = np.abs(a - R[y0:y0+bs, x0+dd:x0+dd+bs]).mean()
            if best is None or s < best[1]: second = best; best = (dd, s)
        if best[1] > 12: continue
        v = best[0] * 2
        col = (255, 255, 255) if v == 0 else ((80, 200, 255) if v > 0 else (255, 120, 80))
        if abs(v) >= 30: col = (60, 120, 255) if v > 0 else (255, 60, 60)
        d.text((x0 + 2, y0 + 6), str(v), fill=col)
out.save(sys.argv[2])
