# Teksturat e Tempullit (origjinale, procedurale, të përsëritshme): pllaka guri të arta, mure guri,
# brez me spirale të gdhendura, tjegulla, shkëmb, re. Çdo teksturë = 4 m x 4 m në lojë.
import numpy as np, sys, os
from PIL import Image
OUT = sys.argv[1]; os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)
N = 1024

def fnoise(n, scale, seed):
    """zhurmë periodike (tileable) me spektër 1/f"""
    r = np.random.default_rng(seed)
    f = np.fft.fftfreq(n)[:, None] ** 2 + np.fft.fftfreq(n)[None, :] ** 2
    f = np.sqrt(f); f[0, 0] = 1
    spec = (r.normal(size=(n, n)) + 1j * r.normal(size=(n, n))) / (f ** scale)
    spec[0, 0] = 0
    x = np.real(np.fft.ifft2(spec))
    return (x - x.mean()) / (x.std() + 1e-6)

def blur(a, k):
    """mjegullim periodik me FFT (gaussian)"""
    n = a.shape[0]
    f = np.fft.fftfreq(n)
    g = np.exp(-(f[:, None] ** 2 + f[None, :] ** 2) * (2 * np.pi * k) ** 2 / 2)
    return np.real(np.fft.ifft2(np.fft.fft2(a) * g))

def normal_from_height(h, strength):
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    n = np.stack([-gx, gy, np.ones_like(h)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return ((n * 0.5 + 0.5) * 255).astype(np.uint8)

def save(name, rgb, h=None, strength=6.0):
    Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8)).save(f"{OUT}/{name}_albedo.png")
    if h is not None:
        Image.fromarray(normal_from_height(h, strength)).save(f"{OUT}/{name}_normal.png")

def stones(rows_h, widths, seed, n=N, gap=0.012):
    """shtrim me gurë drejtkëndëshë të çrregullt; kthen id-në e gurit, distancën nga skaji (m), id e rreshtit"""
    r = np.random.default_rng(seed)
    M = 4.0
    ys = [0.0]
    while ys[-1] < M:
        ys.append(ys[-1] + r.uniform(*rows_h))
    ys = np.array(ys) * (M / ys[-1])
    yy, xx = (np.mgrid[0:n, 0:n] + 0.5) / n * M
    sid = np.zeros((n, n), np.int32); edge = np.full((n, n), 9.0)
    k = 0
    for i in range(len(ys) - 1):
        y0, y1 = ys[i], ys[i + 1]
        xs = [r.uniform(0, 1.0)]
        while xs[-1] < xs[0] + M:
            xs.append(xs[-1] + r.uniform(*widths))
        xs = np.array(xs); xs = xs[0] + (xs - xs[0]) * (M / (xs[-1] - xs[0]))
        rowm = (yy >= y0) & (yy < y1)
        for j in range(len(xs) - 1):
            a, b = xs[j], xs[j + 1]
            xm = ((xx - a) % M)
            m = rowm & (xm < (b - a))
            sid[m] = k
            d = np.minimum.reduce([xm, (b - a) - xm, yy - y0, y1 - yy])
            edge[m] = d[m]
            k += 1
    return sid, edge, k

# ---------- 1. shtegu: pllaka guri ranor të artë ----------
sid, edge, k = stones((0.55, 1.05), (0.6, 1.5), 1)
var = rng.uniform(-1, 1, (k, 3))
base = np.array([0.80, 0.62, 0.30])
col = base[None, None] + 0.07 * var[sid][..., :1] + np.array([0.03, 0.02, -0.02]) * var[sid][..., 1:2]
n1 = fnoise(N, 1.6, 11); n2 = fnoise(N, 0.9, 12)
dirt = np.clip(blur(fnoise(N, 1.2, 13), 6) * 0.5 + 0.5, 0, 1)
bevel = np.clip(edge / 0.045, 0, 1)
grout = edge < 0.012
col = col * (0.88 + 0.06 * n1[..., None] + 0.03 * n2[..., None])
col = col * (0.72 + 0.28 * bevel[..., None])
col = col * (1 - 0.18 * dirt[..., None] * (1 - bevel[..., None] * 0.6))
# njolla myshku në fuga
mossy = (blur(fnoise(N, 1.3, 14), 10) > 0.7) & (edge < 0.06)
col[mossy] = col[mossy] * 0.6 + np.array([0.20, 0.30, 0.10]) * 0.4
col[grout] = np.array([0.22, 0.17, 0.10])
# plasaritje
crack = np.abs(blur(fnoise(N, 1.0, 15), 1.5)) < 0.025
col[crack & ~grout] *= 0.65
h = np.where(grout, 0.0, np.sqrt(np.clip(edge / 0.05, 0, 1))) + 0.04 * n1 + 0.02 * n2 - 0.4 * crack
save("shtegu", col, h, 9.0)

# ---------- 2. muri: blloqe guri gri-blu (si shkëmbinjtë e tempullit) ----------
sid, edge, k = stones((0.38, 0.6), (0.7, 1.4), 2)
var = rng.uniform(-1, 1, (k, 2))
base = np.array([0.50, 0.50, 0.56])
col = base[None, None] + 0.06 * var[sid][..., :1] + np.array([0.03, 0.0, -0.03]) * var[sid][..., 1:2]
n1 = fnoise(N, 1.5, 21); streak = blur(fnoise(N, 1.1, 22), 3)
bevel = np.clip(edge / 0.06, 0, 1); grout = edge < 0.015
col = col * (0.85 + 0.08 * n1[..., None]) * (0.7 + 0.3 * bevel[..., None])
col *= (1 - 0.12 * np.clip(streak, 0, 2)[..., None])
lich = blur(fnoise(N, 1.4, 23), 4) > 1.1
col[lich] = col[lich] * 0.5 + np.array([0.62, 0.58, 0.40]) * 0.5
col[grout] = np.array([0.18, 0.18, 0.2])
h = np.where(grout, 0.0, np.sqrt(np.clip(edge / 0.07, 0, 1))) + 0.06 * n1
save("muri", col, h, 8.0)

# ---------- 3. brez i gdhendur me spirale (për bordurat dhe harqet) ----------
yy, xx = (np.mgrid[0:N, 0:N] + 0.5) / N * 4.0
cx = (xx % 1.0) - 0.5; cy = (yy % 1.0) - 0.5
r_ = np.sqrt(cx ** 2 + cy ** 2); th = np.arctan2(cy, cx)
flip = ((np.floor(xx) + np.floor(yy)) % 2) * 2 - 1
spiral = np.sin((r_ * 2 * np.pi * 5.0) - flip * th) > 0.35
spiral &= r_ < 0.42
frame = (np.abs(cx) > 0.47) | (np.abs(cy) > 0.47)
carve = (spiral | frame).astype(float)
carve = blur(carve, 1.2)
n1 = fnoise(N, 1.5, 31)
col = np.array([0.74, 0.62, 0.40])[None, None] * (0.85 + 0.07 * n1[..., None])
col = col * (1 - 0.35 * carve[..., None])
h = -carve * 1.0 + 0.05 * n1
save("gdhendja", col, h, 10.0)

# ---------- 4. tjegulla portokalli ----------
v = (yy % 0.28) / 0.28; u = (xx % 0.22) / 0.22
row = np.floor(yy / 0.28)
u2 = ((xx + (row % 2) * 0.11) % 0.22) / 0.22
tile = np.sin(u2 * np.pi)
n1 = fnoise(N, 1.4, 41)
col = np.array([0.80, 0.36, 0.16])[None, None] * (0.65 + 0.35 * tile[..., None]) * (0.9 + 0.08 * n1[..., None])
col *= (0.75 + 0.25 * np.clip(v * 3, 0, 1))[..., None]
h = tile * 0.8 + np.clip(v * 3, 0, 1) * 0.4
save("tjegulla", col, h, 6.0)

# ---------- 5. shkëmb (kolonat e mëdha, majat në re) ----------
n1 = fnoise(N, 1.8, 51); n2 = fnoise(N, 1.2, 52)
strata = np.sin(yy * 2 * np.pi * 1.5 + 2.5 * blur(n2, 8)) * 0.5 + 0.5
col = np.array([0.46, 0.46, 0.52])[None, None] * (0.8 + 0.1 * n1[..., None] + 0.12 * strata[..., None])
col *= 1 - 0.15 * np.clip(blur(n2, 2), 0, 2)[..., None]
h = n1 * 0.4 + strata * 0.6 + n2 * 0.2
save("shkembi", col, h, 5.0)

# ---------- 6. retë (alfa) ----------
c = blur(fnoise(512, 1.9, 61), 3)
c = np.clip((c - 0.1) * 0.9, 0, 1)
rgb = np.ones((512, 512, 3)) * np.array([1.0, 0.97, 0.94])
rgb *= (0.85 + 0.15 * c[..., None])
rgba = np.concatenate([rgb, c[..., None]], -1)
Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), "RGBA").save(f"{OUT}/ret_albedo.png")
print("gati")
