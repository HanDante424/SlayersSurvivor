"""가니악 1페이즈 측면 걷기 A(1~4) · B(5~8) → 한 줄 8프레임 시트 (왼쪽을 보는 옆면).
그림은 새로 그리지 않음: 잡티 제거 · B 밝기를 A 에 맞춤 · 시트 전체 같은 배율 · 피봇 정렬.
피봇: 가로 = 얼굴 앞끝 (머리 윗부분의 가장 왼쪽 — 망토 · 검 끝 흔들림에 영향 안 받음), 세로 = 가장 낮은 발 바닥.
usage: python -I build_ganiac_walk.py <A> <B> <out png> [anchor: face|bbox]
"""
import sys, json, numpy as np
from PIL import Image
from scipy import ndimage

A, B, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
MODE = sys.argv[4] if len(sys.argv) > 4 else 'face'
SRC = 627
CELL, BASE, HEIGHT = 400, 12, 372   # 카구츠치 달리기와 같은 규격
PIVOT_X = 0.36                      # [임시] 얼굴 앞끝을 칸 가로의 이 비율에 둠 (몸통이 칸 가운데 오도록)

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
    for i in range(n):
        if sizes[i] >= big * 0.03: continue      # 몸에서 떨어진 작은 조각 (잡티) 제거
        f[lab == i + 1] = 0; rem += int(sizes[i])
    return f, rem

sheets = [load(A), load(B)]
la, lb = lum(sheets[0]), lum(sheets[1])
frames, removed = [], 0
for si, s in enumerate(sheets):
    for r in range(2):
        for c in range(2):
            f, rem = clean(s[r * SRC:(r + 1) * SRC, c * SRC:(c + 1) * SRC].copy()); removed += rem
            if si == 1:   # B 밝기를 A 에 맞춤 (곱하기만, 알파 유지)
                f[:, :, :3] = np.clip(f[:, :, :3] * (la / lb), 0, 255)
            frames.append(f)

def bbox(f):
    ys, xs = np.where(f[:, :, 3] > 128); return xs.min(), ys.min(), xs.max(), ys.max()

def face_x(f):
    x0, y0, x1, y1 = bbox(f)
    ys, xs = np.where(f[:, :, 3] > 128)
    m = ys < y0 + (y1 - y0) * 0.22        # 머리 구간
    return float(xs[m].min())

bbs = [bbox(f) for f in frames]
top = min(b[1] for b in bbs); foot = max(b[3] for b in bbs)
sc = HEIGHT / (foot - top + 1)
if MODE == 'face':
    anchors = [face_x(f) for f in frames]
else:
    anchors = [(b[0] + b[2]) / 2 for b in bbs]
out = Image.new('RGBA', (CELL * 8, CELL), (0, 0, 0, 0))
clip, ax_after, foot_after = [], [], []
for i, f in enumerate(frames):
    im = Image.fromarray(f.astype(np.uint8), 'RGBA').resize((round(SRC * sc), round(SRC * sc)), Image.LANCZOS)
    ox = round(CELL * PIVOT_X - anchors[i] * sc)
    oy = round(CELL - BASE - bbs[i][3] * sc)       # 프레임마다 가장 낮은 발을 같은 선에
    cell = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
    cell.paste(im, (ox, oy), im)
    al = np.asarray(cell)[:, :, 3]
    if al[0].max() > 30 or al[-1].max() > 30 or al[:, 0].max() > 30 or al[:, -1].max() > 30: clip.append(i + 1)
    out.paste(cell, (i * CELL, 0))
    ys, xs = np.where(al > 128)
    foot_after.append(int(ys.max())); ax_after.append(round(face_x(np.asarray(cell).astype(np.float32)), 1))
out.save(OUT, optimize=True)
raw_anchor = [round(face_x(f), 1) for f in frames]
raw_foot = [int(b[3]) for b in bbs]
print(json.dumps(dict(lum_A=round(la, 1), lum_B=round(lb, 1), lum_B_after=round(lum(np.asarray(out.crop((CELL*4,0,CELL*8,CELL))).astype(np.float32)), 1),
                      mode=MODE, scale=round(sc, 4), size=out.size, edge_touch=clip, edge_bits_removed=removed,
                      face_x_raw=raw_anchor, face_x_after=ax_after, foot_raw=raw_foot, foot_after=foot_after), ensure_ascii=False))
