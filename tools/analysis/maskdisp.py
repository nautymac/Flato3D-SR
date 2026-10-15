# maskdisp.py <frame.bmp> name=x0,y0,x1,y1[,purple] ...: disparity (full-res px) of regions in half-res SBS dump coords
import sys
import numpy as np
from PIL import Image
im = np.asarray(Image.open(sys.argv[1]).convert('RGB'), dtype=np.float32)
H, W2, _ = im.shape; W = W2 // 2
L, R = im[:, :W], im[:, W:]
def md(x0, y0, x1, y1, purple, maxd=40):
    a = L[y0:y1, x0:x1]
    m = (((a[:, :, 2] > a[:, :, 1] + 25) & (a[:, :, 0] > a[:, :, 1] + 10)) if purple else np.ones(a.shape[:2], bool)).astype(np.float32)
    best = None
    for d in range(-maxd, maxd + 1):
        if x0 + d < 0 or x1 + d > W: continue
        b = R[y0:y1, x0 + d:x1 + d]
        s = (np.abs(a - b).sum(axis=2) * m).sum() / max(m.sum(), 1)
        if best is None or s < best[1]: best = (d, s)
    return best[0] * 2, round(float(best[1]), 1), int(m.sum())
for arg in sys.argv[2:]:
    name, spec = arg.split('=')
    p = spec.split(',')
    print(name, md(int(p[0]), int(p[1]), int(p[2]), int(p[3]), len(p) > 4))
