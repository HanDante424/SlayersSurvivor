"""AI 생성 스프라이트 시트를 자동 분석해 균일한 그리드로 재정렬.
usage: python repack_sheet.py <in.png> <out.png> <rows> <cols> [--analyze]
- 알파로 각 캐릭터 영역을 찾아냄 (행 → 열 순)
- 모든 프레임을 같은 칸 크기로, 발(바닥) 기준 + 가로 중심 정렬
"""
import sys
import numpy as np
from PIL import Image

src, dst, ROWS, COLS = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
analyze = "--analyze" in sys.argv

img = Image.open(src).convert("RGBA")
a = np.array(img)
# 배경에 남은 거의 투명한 잡티 제거
a[a[:, :, 3] < 16] = 0
alpha = a[:, :, 3]
H, W = alpha.shape
print(f"size {W}x{H}")
hist = np.histogram(alpha, bins=[0, 1, 16, 64, 128, 200, 255, 256])[0]
print("alpha hist [0,1-15,16-63,64-127,128-199,200-254,255]:", hist.tolist())

SOLID = 48


def bands(mask_1d, want, min_gap=3):
    idx = np.where(mask_1d)[0]
    if len(idx) == 0:
        return []
    groups, start, prev = [], idx[0], idx[0]
    for i in idx[1:]:
        if i - prev > min_gap:
            groups.append((start, prev + 1))
            start = i
        prev = i
    groups.append((start, prev + 1))
    # 작은 조각(먼지) 제거 후 큰 것부터 want개
    groups = [g for g in groups if g[1] - g[0] > 10]
    if len(groups) > want:
        # 가까운 것끼리 병합: 가장 작은 간격부터
        while len(groups) > want:
            gaps = [groups[i + 1][0] - groups[i][1] for i in range(len(groups) - 1)]
            k = int(np.argmin(gaps))
            groups[k] = (groups[k][0], groups[k + 1][1])
            del groups[k + 1]
    return groups


solid = alpha >= SOLID
row_bands = bands(solid.any(axis=1), ROWS, min_gap=4)
print("rows:", row_bands)
assert len(row_bands) == ROWS, f"expected {ROWS} rows, got {len(row_bands)}"

frames = []
for r, (y0, y1) in enumerate(row_bands):
    col_bands = bands(solid[y0:y1].any(axis=0), COLS, min_gap=4)
    print(f"row {r} cols ({len(col_bands)}):", col_bands)
    assert len(col_bands) == COLS, f"row {r}: expected {COLS} cols, got {len(col_bands)}"
    row = []
    for (x0, x1) in col_bands:
        # 넉넉히 잘라낸 뒤 실제 내용(alpha>0) bbox로 다시 조임
        pad = 6
        X0, X1 = max(0, x0 - pad), min(W, x1 + pad)
        Y0, Y1 = max(0, y0 - pad), min(H, y1 + pad)
        sub = a[Y0:Y1, X0:X1].copy()
        m = sub[:, :, 3] >= 8
        ys, xs = np.where(m)
        sub = sub[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
        # 단단한 픽셀 기준 가로 중심 (망토 끝 반투명에 덜 흔들리게)
        sm = sub[:, :, 3] >= 128
        cx = np.where(sm)[1].mean() if sm.any() else sub.shape[1] / 2
        row.append((sub, cx))
    frames.append(row)

maxw = max(s.shape[1] for row in frames for s, _ in row)
maxh = max(s.shape[0] for row in frames for s, _ in row)
CELL = int(np.ceil(max(maxw, maxh) * 1.15 / 4) * 4)
print(f"max frame {maxw}x{maxh} -> cell {CELL}")
if analyze:
    sys.exit(0)

out = np.zeros((CELL * ROWS, CELL * COLS, 4), dtype=np.uint8)
bottom_margin = int(CELL * 0.06)
for r, row in enumerate(frames):
    for c, (sub, cx) in enumerate(row):
        h, w = sub.shape[:2]
        ox = int(round(c * CELL + CELL / 2 - cx))
        oy = r * CELL + CELL - bottom_margin - h
        ox = min(max(ox, c * CELL), c * CELL + CELL - w)
        region = out[oy:oy + h, ox:ox + w]
        region[:] = np.where(sub[:, :, 3:4] > 0, sub, region)

Image.fromarray(out).save(dst)
print("saved", dst, out.shape[1], "x", out.shape[0], "cell", CELL)
