"""가니악 정지 · 걷기 · 베기 → 게임 시트 (행 = 아래 · 왼쪽 · 오른쪽 · 위, 오른쪽 = 왼쪽 좌우 반전).
그림을 새로 그리지 않음: 옅은 잡티 제거 · 방향별 밝기 맞춤 · 크기 조절 · 위치 이동 · 칸 넘침 조각 제거만.
usage: python -I build_ganiac.py <scratchpad> <out dir>
"""
import sys, os, json, numpy as np
from PIL import Image
from scipy import ndimage

S, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
SRC_CELL = 627
CELL, BASE = 384, 24          # 정지 · 걷기 칸, 발끝 (칸 아래에서)
SCELL, SBASE = 480, 30        # 베기 칸 (검 공간), 발끝
HEIGHT = 300                  # 몸 키 (머리카락 끝 ~ 발끝)
ALPHA_CUT = 12                # 이 이하 알파 = 잡티 → 0

def P(*a): return os.path.join(S, *a)
DIRS = ['Down', 'Left', 'Up']
idle_src = {'Down': P('ganiac-idle2', 'Idle_Down_v2.png'), 'Left': P('ganiac-idle2', 'Idle_Left_v2.png'), 'Up': P('ganiac-idle2', 'Idle_Up_v2.png')}
walk_src = {'Down': [P('ganiac-walk-png', 'Walk_Down_A.png'), P('ganiac-walk-png', 'Walk_Down_B.png')],
            'Left': [P('ganiac-walk-png', 'Walk_Left_A.png'), P('ganiac-walk-png', 'Walk_Left_B.png')],
            'Up':   [P('ganiac-walk-png', 'Walk_Up_A.png'), P('ganiac-walk-png', 'Walk_Up_B.png')]}   # 후방은 원본에서 다시 보정
slash_src = {'Down': [P('ganiac-slash2', 'Slash_Down_A.png'), P('ganiac-slash2', 'Slash_Down_B.png')],
             'Left': [P('ganiac-slash2', 'Slash_Left_A.png'), P('ganiac-slash2', 'Slash_Left_B.png')],
             'Up':   [P('ganiac-slash3', 'Slash_Up_A.png'), P('ganiac-slash3', 'Slash_Up_B.png')]}

def load(p):
    a = np.asarray(Image.open(p).convert('RGBA')).astype(np.float32)
    a[a[:, :, 3] <= ALPHA_CUT] = 0
    return a

def lum(a):
    m = a[:, :, 3] > 200
    return float((0.299 * a[:, :, 0] + 0.587 * a[:, :, 1] + 0.114 * a[:, :, 2])[m].mean())

def bright(a, target):
    k = target / lum(a)
    b = a.copy(); b[:, :, :3] = np.clip(b[:, :, :3] * k, 0, 255)
    return b, k

def cells(a):
    return [a[r * SRC_CELL:(r + 1) * SRC_CELL, c * SRC_CELL:(c + 1) * SRC_CELL] for r in range(2) for c in range(2)]

def clean_edges(f):
    """칸 가장자리에 닿은 작은 조각 (옆 칸에서 넘어온 검기 등) 제거"""
    mask = f[:, :, 3] > 10
    lab, n = ndimage.label(mask)
    if n <= 1: return f, 0
    sizes = ndimage.sum(mask, lab, range(1, n + 1))
    big = sizes.max(); removed = 0
    h, w = mask.shape
    for i in range(n):
        if sizes[i] >= big * 0.03: continue
        ys, xs = np.where(lab == i + 1)
        if xs.min() <= 2 or xs.max() >= w - 3 or ys.min() <= 2 or ys.max() >= h - 3:
            f[lab == i + 1] = 0; removed += int(sizes[i])
    return f, removed

def bbox(f):
    ys, xs = np.where(f[:, :, 3] > 128)
    return xs.min(), ys.min(), xs.max(), ys.max()

def ref_center_x(f):
    x0, y0, x1, y1 = bbox(f)
    top = f[y0:y0 + int((y1 - y0) * 0.45), :, 3] > 128   # 머리 · 어깨 (검 제외)
    ys, xs = np.where(top)
    return float(xs.mean())

def place(f, scale, cx, foot, cell, base):
    """원본 프레임 f 를 scale 배 해서 (cx, foot) 가 칸의 (가운데, 아래-base) 에 오게"""
    h, w = f.shape[:2]
    im = Image.fromarray(f.astype(np.uint8), 'RGBA').resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)
    out = Image.new('RGBA', (cell, cell), (0, 0, 0, 0))
    ox = round(cell / 2 - cx * scale); oy = round((cell - base) - foot * scale)
    out.alpha_composite(im, (0, 0)) if False else out.paste(im, (ox, oy), im)
    return out

report = {}
idle_lum = {}
sheets = {'Idle': {}, 'Run': {}, 'Slash': {}}
for d in DIRS:
    a = load(idle_src[d]); idle_lum[d] = lum(a)
    f, rem = clean_edges(a)
    x0, y0, x1, y1 = bbox(f); sc = HEIGHT / (y1 - y0 + 1)
    sheets['Idle'][d] = [place(f, sc, ref_center_x(f), y1, CELL, BASE)]
    report[f'Idle_{d}'] = dict(scale=round(sc, 4), lum=round(idle_lum[d], 1))

for d in DIRS:
    frames = []
    for half, p in enumerate(walk_src[d]):
        a = load(p); a, k = bright(a, idle_lum[d])
        fs = [clean_edges(c.copy())[0] for c in cells(a)]
        hs = [bbox(f)[3] - bbox(f)[1] + 1 for f in fs]
        sc = HEIGHT / float(np.median(hs))
        cx = float(np.median([ref_center_x(f) for f in fs]))
        for f in fs:
            frames.append(place(f, sc, cx, bbox(f)[3], CELL, BASE))   # 걷기: 프레임마다 발끝 고정
        report[f'Run_{d}_{"AB"[half]}'] = dict(scale=round(sc, 4), bright=round(k, 3))
    sheets['Run'][d] = frames

for d in DIRS:
    frames = []
    for half, p in enumerate(slash_src[d]):
        a = load(p); a, k = bright(a, idle_lum[d])
        fs = []; removed = 0
        for c in cells(a):
            f, r = clean_edges(c.copy()); fs.append(f); removed += r
        rest = fs[0] if half == 0 else fs[3]                   # 선 자세 프레임 (A 1번 · B 4번)
        x0, y0, x1, y1 = bbox(rest)
        sc = HEIGHT / (y1 - y0 + 1); cx = ref_center_x(rest); foot = y1
        for f in fs:
            frames.append(place(f, sc, cx, foot, SCELL, SBASE))   # 베기: 시트 단위로 같은 이동 (발은 거의 고정)
        report[f'Slash_{d}_{"AB"[half]}'] = dict(scale=round(sc, 4), bright=round(k, 3), edge_bits_removed=removed)
    sheets['Slash'][d] = frames

def assemble(name, rowsdict, cell):
    order = [rowsdict['Down'], rowsdict['Left'], [f.transpose(Image.FLIP_LEFT_RIGHT) for f in rowsdict['Left']], rowsdict['Up']]
    cols = len(order[0])
    sheet = Image.new('RGBA', (cols * cell, 4 * cell), (0, 0, 0, 0))
    clip = []
    for r, row in enumerate(order):
        for c, f in enumerate(row):
            sheet.paste(f, (c * cell, r * cell))
            al = np.asarray(f)[:, :, 3]
            if al[0].max() > 30 or al[-1].max() > 30 or al[:, 0].max() > 30 or al[:, -1].max() > 30: clip.append((r, c))
    sheet.save(os.path.join(OUT, f'Ganiac_{name}.png'), optimize=True)
    return sheet.size, clip

for name, cell in [('Idle', CELL), ('Run', CELL), ('Slash', SCELL)]:
    size, clip = assemble(name, sheets[name], cell)
    report[f'sheet_{name}'] = dict(size=size, edge_touch=clip)
print(json.dumps(report, ensure_ascii=False, indent=1))
