"""가니악 1페이즈 횡베기 자세 (왼쪽을 보는 옆면 한 장) → 640×400 칸. 그림은 새로 그리지 않음: 배율 · 위치만.
배율 = 돌진 자세와 같음 (머리 크기 기준), 얼굴 앞끝 = 칸 중심 −71px (대기 · 돌진과 같은 자리), 발 = 칸 아래 12px.
usage: python -I build_ganiac_p1_slash.py <src png> <out png>
"""
import sys, json, numpy as np
from PIL import Image
SRC, OUT = sys.argv[1], sys.argv[2]
CW, CH, BASE = 640, 400, 12
SC = 0.335          # [임시] 돌진 자세와 같은 배율
FACE_OFF = -71      # 얼굴 앞끝 = 칸 중심 + FACE_OFF
im = Image.open(SRC).convert('RGBA')
a = np.asarray(im).astype(np.float32); a[a[:, :, 3] <= 12] = 0
ys, xs = np.where(a[:, :, 3] > 128)
x0, y0, x1, y1 = xs.min(), ys.min(), xs.max(), ys.max()
m = ys < y0 + (y1 - y0) * 0.22
face = float(xs[m].min())
im = Image.fromarray(a.astype(np.uint8), 'RGBA')
w, h = round(im.width * SC), round(im.height * SC)
im = im.resize((w, h), Image.LANCZOS)
ox = round(CW / 2 + FACE_OFF - face * SC)
oy = round(CH - BASE - y1 * SC)
cell = Image.new('RGBA', (CW, CH), (0, 0, 0, 0)); cell.paste(im, (ox, oy), im)
cell.save(OUT, optimize=True)
al = np.asarray(cell)[:, :, 3]; yy, xx = np.where(al > 128)
print(json.dumps(dict(src_bbox=[int(x0), int(y0), int(x1), int(y1)], face_x_src=face, scale=SC, offset=[ox, oy],
      out_bbox=[int(xx.min()), int(yy.min()), int(xx.max()), int(yy.max())],
      edge_touch=bool(al[0].max() > 30 or al[-1].max() > 30 or al[:, 0].max() > 30 or al[:, -1].max() > 30))))
