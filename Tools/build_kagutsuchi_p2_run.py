"""카구츠치 2페이즈 달리기 A(1~4) · B(5~8) → 한 줄 8프레임 시트 (왼쪽을 보는 옆면).
그림을 새로 그리지 않음: 잡티 제거 · 칸 넘침 조각 제거 · 시트 전체 같은 배율 · 같은 위치 이동 (달리기 위아래 움직임 유지).
usage: python -I build_kagu_run.py <A> <B> <out png>
"""
import sys, json, numpy as np
from PIL import Image
from scipy import ndimage

A, B, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
SRC = 627
CELL, BASE, HEIGHT = 400, 12, 372   # 칸 · 가장 낮은 발끝 (칸 아래에서) · 머리카락 끝 ~ 발끝 최대 높이

def load(p):
    a = np.asarray(Image.open(p).convert('RGBA')).astype(np.float32)
    a[a[:, :, 3] <= 12] = 0
    return a

def lum(a):
    m = a[:, :, 3] > 200
    return float((0.299 * a[:, :, 0] + 0.587 * a[:, :, 1] + 0.114 * a[:, :, 2])[m].mean())

def clean(f):
    mask = f[:, :, 3] > 10
    lab, n = ndimage.label(mask)
    if n <= 1: return f, 0
    sizes = ndimage.sum(mask, lab, range(1, n + 1)); big = sizes.max(); rem = 0
    h, w = mask.shape
    for i in range(n):
        if sizes[i] >= big * 0.03: continue
        ys, xs = np.where(lab == i + 1)
        if xs.min() <= 2 or xs.max() >= w - 3 or ys.min() <= 2 or ys.max() >= h - 3:
            f[lab == i + 1] = 0; rem += int(sizes[i])
    return f, rem

sheets = [load(A), load(B)]
la, lb = lum(sheets[0]), lum(sheets[1])
frames, removed = [], 0
for s in sheets:
    for r in range(2):
        for c in range(2):
            f, rem = clean(s[r * SRC:(r + 1) * SRC, c * SRC:(c + 1) * SRC].copy()); frames.append(f); removed += rem

def bbox(f):
    ys, xs = np.where(f[:, :, 3] > 128); return xs.min(), ys.min(), xs.max(), ys.max()

bbs = [bbox(f) for f in frames]
top = min(b[1] for b in bbs); foot = max(b[3] for b in bbs)
left = min(b[0] for b in bbs); right = max(b[2] for b in bbs)
sc = HEIGHT / (foot - top + 1)
cx = (left + right) / 2
out = Image.new('RGBA', (CELL * 8, CELL), (0, 0, 0, 0))
clip = []
for i, f in enumerate(frames):
    im = Image.fromarray(f.astype(np.uint8), 'RGBA').resize((round(SRC * sc), round(SRC * sc)), Image.LANCZOS)
    cell = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
    cell.paste(im, (round(CELL / 2 - cx * sc), round(CELL - BASE - foot * sc)), im)
    al = np.asarray(cell)[:, :, 3]
    if al[0].max() > 30 or al[-1].max() > 30 or al[:, 0].max() > 30 or al[:, -1].max() > 30: clip.append(i + 1)
    out.paste(cell, (i * CELL, 0))
out.save(OUT, optimize=True)
fb = [(b[3] - foot) * sc for b in bbs]
print(json.dumps(dict(lum_A=round(la, 1), lum_B=round(lb, 1), scale=round(sc, 4), size=out.size, edge_touch=clip,
                      edge_bits_removed=removed, foot_offset_px=[round(x, 1) for x in fb]), ensure_ascii=False))
