# Teksturat shtesë: ujë (normal + ngjyrë), lëvore peme, shtrat lumi me guralecë, bar i verdhë
import numpy as np, sys
from PIL import Image
sys.argv=[sys.argv[0], sys.argv[1]]
exec(open('gen_tex.py').read().split("# ---------- 1.")[0])
N = 1024
yy, xx = (np.mgrid[0:N, 0:N] + 0.5) / N * 4.0
# ujë: valëza
w = sum(np.sin((xx * np.cos(a) + yy * np.sin(a)) * 2 * np.pi * f + p) / f for a, f, p in
        [(0.3, 2, 0), (1.4, 3, 1), (2.2, 5, 2), (0.9, 7, 3), (2.9, 4, 4)])
w = w + 0.6 * blur(fnoise(N, 1.4, 71), 2)
Image.fromarray(normal_from_height(w * 0.08, 6.0)).save(f"{OUT}/uji_normal.png")
col = np.array([0.20, 0.45, 0.45])[None, None] * (0.9 + 0.1 * np.clip(w * 0.3, -1, 1))[..., None]
Image.fromarray((np.clip(col, 0, 1) * 255).astype(np.uint8)).save(f"{OUT}/uji_albedo.png")
# lëvore: vija vertikale (në UV: v përgjatë trungut)
n1 = fnoise(N, 1.3, 81)
ridges = np.abs(np.sin(xx * 2 * np.pi * 3 + 3 * blur(fnoise(N, 1.0, 82), 6)))
h = ridges * 0.8 + n1 * 0.2
col = np.array([0.36, 0.26, 0.17])[None, None] * (0.55 + 0.5 * ridges[..., None]) * (0.9 + 0.08 * n1[..., None])
moss = blur(fnoise(N, 1.3, 83), 6) > 0.6
col[moss] = col[moss] * 0.4 + np.array([0.25, 0.38, 0.12]) * 0.6
save("levorja", col, h, 8.0)
# shtrati i lumit: guralecë
sid, edge, k = stones((0.18, 0.35), (0.2, 0.45), 9)
var = np.random.default_rng(9).uniform(-1, 1, (k, 3))
col = np.array([0.42, 0.40, 0.36])[None, None] + 0.08 * var[sid][..., :1] + 0.02 * var[sid]
bev = np.clip(edge / 0.06, 0, 1)
col = col * (0.6 + 0.4 * bev[..., None])
save("shtrati", col, np.sqrt(bev), 8.0)
# bar i verdhë (anash shtegut, alfa)
g = np.zeros((512, 512, 4))
rng = np.random.default_rng(5)
for i in range(260):
    x0 = rng.uniform(0, 512); hgt = rng.uniform(200, 500); lean = rng.uniform(-0.25, 0.25); wdt = rng.uniform(4, 9)
    for yy_ in range(int(512 - hgt), 512):
        t = (512 - yy_) / hgt
        x = x0 + lean * (512 - yy_) + 30 * lean * t * t
        ww = wdt * (1 - t)
        a0, a1 = int(max(0, x - ww)), int(min(511, x + ww))
        if a1 > a0:
            c = np.array([0.55 + 0.35 * t, 0.55 + 0.25 * t, 0.12, 1.0])
            g[yy_, a0:a1] = c
Image.fromarray((np.clip(g, 0, 1) * 255).astype(np.uint8), "RGBA").save(f"{OUT}/bari_albedo.png")
print("gati 2")
