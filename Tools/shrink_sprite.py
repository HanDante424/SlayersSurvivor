"""
스프라이트 화질 통일용 줄인 복사본 만들기 (2026-11-22 사용자 합의).

큰 단일 그림 (스탠드 · 연출 포즈 등) 은 화면에서 몇 배 줄여 그려져서, 밉맵 없이 쓰면 선명하게 지글거리고 (스탠드와 질감이 달라 보임)
밉맵을 켜면 무압축이라 용량이 크게 늘어남 (그림당 약 10MB). 그래서 원본은 그대로 두고 1/3 로 줄인 복사본 ({이름}_s3.png) 을 만들어 쓴다:
  모습은 밉맵을 켠 원본과 거의 같고 용량은 약 1/9.
- 알파를 곱한 색으로 줄임 (투명한 곳의 색이 가장자리로 번지지 않게). LANCZOS.
- 원본 파일은 건드리지 않음. 같은 이름의 복사본이 있으면 덮어쓰므로 주의.
- 코드에서는 Player.LoadShrinkable 처럼 "복사본이 있으면 그것, 없으면 원본" 으로 불러오고, 그림 픽셀 좌표 (발 위치 · 기준점) 는 줄인 배율로 나눠 계산.
  배율 (--factor) 을 바꾸면 코드의 ShrinkFactor 도 같이 바꿀 것 (복사본 이름의 숫자 = 배율).

사용:  python -I Tools/shrink_sprite.py <그림.png> [<그림2.png> ...] [--factor 3] [--out <폴더>]
  --out 을 주지 않으면 원본과 같은 폴더에 {이름}_s{배율}.png 로 저장.
"""
import argparse
import os
import sys

import numpy as np
from PIL import Image


def shrink(path, factor, out_dir=None):
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    nw, nh = max(1, round(w / factor)), max(1, round(h / factor))
    a = np.asarray(im).astype(np.float32)
    al = a[:, :, 3:4] / 255.0
    pm = np.concatenate([a[:, :, :3] * al, a[:, :, 3:4]], axis=2)   # 알파 곱한 색
    rs = np.stack([np.asarray(Image.fromarray(pm[:, :, i]).resize((nw, nh), Image.LANCZOS)) for i in range(4)], axis=2)
    rs = np.clip(rs, 0, 255)
    alpha = rs[:, :, 3:4] / 255.0
    rgb = np.where(alpha > 1e-4, rs[:, :, :3] / np.maximum(alpha, 1e-4), 0)
    out = np.concatenate([np.clip(rgb, 0, 255), rs[:, :, 3:4]], axis=2).astype(np.uint8)
    base = os.path.splitext(os.path.basename(path))[0]
    dest_dir = out_dir or os.path.dirname(os.path.abspath(path))
    os.makedirs(dest_dir, exist_ok=True)
    dest = os.path.join(dest_dir, f"{base}_s{int(factor) if float(factor).is_integer() else factor}.png")
    Image.fromarray(out, "RGBA").save(dest)
    before = w * h * 4 * 1.333 / 1048576
    after = nw * nh * 4 * 1.333 / 1048576
    print(f"{base}: {w}x{h} -> {nw}x{nh}  (밉맵 + 무압축 기준 {before:.1f}MB -> {after:.1f}MB)  => {dest}")
    return dest


def main():
    ap = argparse.ArgumentParser(description="줄인 복사본 만들기")
    ap.add_argument("images", nargs="+")
    ap.add_argument("--factor", type=float, default=3.0)
    ap.add_argument("--out", default=None)
    args = ap.parse_args()
    for p in args.images:
        if not os.path.isfile(p):
            print("없음:", p, file=sys.stderr)
            continue
        shrink(p, args.factor, args.out)


if __name__ == "__main__":
    main()
