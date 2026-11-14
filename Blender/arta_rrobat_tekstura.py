# Rrobat e Artës: bluza liri ngjyrë kremi (pa logo) + pantallona kanavace kafe, nga tekstura CC0 e MakeHuman
import numpy as np, sys, os
from PIL import Image, ImageFilter
src, out = sys.argv[1], sys.argv[2]
im = np.asarray(Image.open(src).convert("RGB")).astype(np.float32) / 255
H, W, _ = im.shape
yy, xx = np.mgrid[0:H, 0:W] / np.array([H, W])[:, None, None]
mx, mn = im.max(2), im.min(2)
sat = (mx - mn) / (mx + 1e-4)
bg = (sat < 0.12) & (mx > 0.6)
lum = im @ np.array([0.3, 0.59, 0.11], np.float32)
from scipy.ndimage import binary_fill_holes
shirt = binary_fill_holes(((yy < 0.43) | ((xx > 0.72) & (yy > 0.58))) & ~bg)
pants = ~shirt & ~bg
rng = np.random.default_rng(3)
# bluza: pëlhurë e re me thurje të imët (heq logon dhe vijat portokalli)
weave = 0.92 + 0.05 * np.sin(xx * W * 1.9) * np.sin(yy * H * 1.9) + rng.normal(0, 0.025, (H, W))
noise = np.asarray(Image.fromarray((rng.random((H // 32, W // 32)) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)) / 255
cream = np.array([0.80, 0.72, 0.57], np.float32)
shirt_col = cream[None, None] * (weave * (0.94 + 0.07 * noise))[..., None]
# pak pluhur/papastërti lart e poshtë
# pantallonat: ruaj detajet (qepjet, xhepat, fërkimet) nga ndriçimi, ngjyrë kafe-kaki
detail = lum / (np.asarray(Image.fromarray((lum * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(24))) / 255 + 1e-3)
detail = np.clip(detail, 0.82, 1.3)
khaki = np.array([0.47, 0.37, 0.24], np.float32)
pants_col = khaki[None, None] * (detail * (0.85 + 0.25 * noise))[..., None]
res = im.copy()
res[shirt] = shirt_col[shirt]
res[pants] = pants_col[pants]
Image.fromarray((np.clip(res, 0, 1) * 255).astype(np.uint8)).save(out)
print("ok", out)
