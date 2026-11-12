"""GPT 스프라이트 묶음(zip 또는 폴더) 검사.

usage:
  python -I inspect_bundle.py <zip 또는 폴더> <작업 폴더> [--rows R --cols C] [--sheet 이름.png]

- zip 이면 <작업 폴더>/src 에 풀고 (기존 파일 덮어쓰지 않음), 폴더면 그대로 읽음
- manifest.json 이 있으면 rows · cols · cell · fps 를 거기서 읽고, 없으면 --rows/--cols 또는 크기로 추정
- 출력: <작업 폴더>/report.md (표) · <작업 폴더>/previews/<시트>_grid.png (칸 경계 · 알파 박스 · 발 위치 표시)

그림은 바꾸지 않음. 측정만 함.
"""
import argparse
import json
import os
import statistics
import zipfile

from PIL import Image, ImageDraw

SOLID = 40   # 몸 · 발 측정 기준 알파 (빛 번짐 제외)
KEEP = 4     # 잘림 · 여백 측정 기준 알파 (빛 번짐 포함)
NOISE = 16   # 이보다 옅은 픽셀 = 잡티 후보


def mask_bbox(im, th):
    return im.getchannel("A").point(lambda v: 255 if v > th else 0).getbbox()


def extract(src, work):
    if os.path.isdir(src):
        return src
    out = os.path.join(work, "src")
    os.makedirs(out, exist_ok=True)
    with zipfile.ZipFile(src) as z:
        for info in z.infolist():
            name = info.filename
            # zip 밖으로 나가는 경로 방지
            dest = os.path.normpath(os.path.join(out, name))
            if not dest.startswith(os.path.normpath(out)) or info.is_dir():
                continue
            if os.path.exists(dest):
                continue
            os.makedirs(os.path.dirname(dest), exist_ok=True)
            with z.open(info) as r, open(dest, "wb") as w:
                w.write(r.read())
    return out


def find_manifests(root):
    found = []
    for d, _, files in os.walk(root):
        for f in files:
            if f.lower() == "manifest.json":
                try:
                    with open(os.path.join(d, f), encoding="utf-8") as fp:
                        found.append((d, json.load(fp)))
                except Exception as e:
                    found.append((d, {"_error": str(e)}))
    return found


def grid_from_manifest(m, sheet_name):
    """manifest 에서 (rows, cols, fps) 를 최대한 읽음. 형식이 여러 가지라 아는 키만 봄"""
    def rc(obj):
        g = obj.get("grid") or {}
        rows = g.get("rows") or obj.get("rows")
        cols = g.get("columns") or g.get("cols") or obj.get("columns") or obj.get("cols")
        lay = obj.get("layout")
        if isinstance(lay, str) and "x" in lay:  # "4 columns x 1 row"
            try:
                a, b = lay.split("x")
                cols = cols or int(a.split()[0]); rows = rows or int(b.split()[0])
            except Exception:
                pass
        return rows, cols, obj.get("fps")

    if m.get("sheet") and os.path.basename(m["sheet"]) == sheet_name:
        return rc(m)
    for key in ("actions", "standing", "move"):
        v = m.get(key)
        items = v.items() if isinstance(v, dict) and key == "actions" else [(key, v)]
        for _, obj in items:
            if isinstance(obj, dict) and os.path.basename(str(obj.get("sheet", ""))) == sheet_name:
                return rc(obj)
    return None, None, None


def cell_sizes(obj, out):
    """manifest 어디에 있든 칸 크기 후보 수집: cell_size [w,h] · cell {width,height} · cellWidth/cellHeight"""
    if isinstance(obj, dict):
        cs = obj.get("cell_size")
        if isinstance(cs, list) and len(cs) == 2 and all(isinstance(v, (int, float)) for v in cs):
            out.append((int(cs[0]), int(cs[1])))
        c = obj.get("cell")
        if isinstance(c, dict) and c.get("width") and c.get("height"):
            out.append((int(c["width"]), int(c["height"])))
        if isinstance(c, list) and len(c) == 2 and all(isinstance(v, (int, float)) for v in c):
            out.append((int(c[0]), int(c[1])))
        if obj.get("cellWidth") and obj.get("cellHeight"):
            out.append((int(obj["cellWidth"]), int(obj["cellHeight"])))
        for v in obj.values():
            cell_sizes(v, out)
    elif isinstance(obj, list):
        for v in obj:
            cell_sizes(v, out)
    return out


def guess_grid(w, h):
    """정사각 칸 가정으로 추정 (실패하면 1×1)"""
    for cell in sorted({w // c for c in range(1, 17) if w % c == 0}, reverse=True):
        if h % cell == 0 and cell >= 64:
            return h // cell, w // cell
    return 1, 1


def inspect_sheet(path, rows, cols, preview_dir):
    im = Image.open(path).convert("RGBA")
    W, H = im.size
    cw, ch = W / cols, H / rows
    a = im.getchannel("A")
    hist = a.histogram()
    noise = sum(hist[1:NOISE])
    cells = []
    for r in range(rows):
        for c in range(cols):
            box = (round(c * cw), round(r * ch), round((c + 1) * cw), round((r + 1) * ch))
            cell = im.crop(box)
            kb, sb = mask_bbox(cell, KEEP), mask_bbox(cell, SOLID)
            if kb is None:
                cells.append({"r": r, "c": c, "empty": True})
                continue
            w, h = cell.size
            touch = [s for s, hit in (("L", kb[0] <= 0), ("T", kb[1] <= 0), ("R", kb[2] >= w), ("B", kb[3] >= h)) if hit]
            sb = sb or kb
            cells.append({
                "r": r, "c": c, "empty": False, "box": box, "keep": kb, "solid": sb,
                "touch": touch,
                "cx": (sb[0] + sb[2]) / 2 - w / 2, "cy": (sb[1] + sb[3]) / 2 - h / 2,
                "foot": h - sb[3], "height": sb[3] - sb[1], "width": sb[2] - sb[0],
                "margin": min(kb[0], kb[1], w - kb[2], h - kb[3]),
            })

    # 미리보기: 칸 경계(회색) · 알파 박스(초록) · 몸 박스(노랑) · 발(빨강) · 잘림 칸(빨간 테두리)
    pv = Image.new("RGBA", im.size, (40, 40, 48, 255))
    checker = Image.new("RGBA", im.size, (0, 0, 0, 0))
    dc = ImageDraw.Draw(checker)
    step = 16
    for y in range(0, H, step):
        for x in range(0, W, step):
            if (x // step + y // step) % 2:
                dc.rectangle((x, y, x + step - 1, y + step - 1), fill=(56, 56, 66, 255))
    pv.alpha_composite(checker)
    pv.alpha_composite(im)
    d = ImageDraw.Draw(pv)
    for e in cells:
        if e["empty"]:
            continue
        x0, y0 = e["box"][0], e["box"][1]
        k, s = e["keep"], e["solid"]
        d.rectangle((x0 + k[0], y0 + k[1], x0 + k[2] - 1, y0 + k[3] - 1), outline=(80, 220, 120, 255))
        d.rectangle((x0 + s[0], y0 + s[1], x0 + s[2] - 1, y0 + s[3] - 1), outline=(240, 210, 60, 255))
        d.line((x0 + s[0], y0 + s[3], x0 + s[2], y0 + s[3]), fill=(255, 60, 60, 255), width=2)
        if e["touch"]:
            b = e["box"]
            d.rectangle((b[0] + 1, b[1] + 1, b[2] - 2, b[3] - 2), outline=(255, 40, 40, 255), width=4)
    for r in range(rows + 1):
        d.line((0, round(r * ch), W, round(r * ch)), fill=(150, 150, 160, 255))
    for c in range(cols + 1):
        d.line((round(c * cw), 0, round(c * cw), H), fill=(150, 150, 160, 255))
    scale = min(1.0, 2048 / max(W, H))
    if scale < 1:
        pv = pv.resize((round(W * scale), round(H * scale)), Image.LANCZOS)
    os.makedirs(preview_dir, exist_ok=True)
    pv_path = os.path.join(preview_dir, os.path.splitext(os.path.basename(path))[0] + "_grid.png")
    pv.save(pv_path)
    return {"size": (W, H), "cell": (cw, ch), "noise": noise, "cells": cells, "preview": pv_path}


def spread(vals):
    return (max(vals) - min(vals)) if vals else 0


def report_sheet(name, info, rows, cols, fps, src_of_grid):
    W, H = info["size"]
    cw, ch = info["cell"]
    full = [e for e in info["cells"] if not e["empty"]]
    empty = [f"{e['r']},{e['c']}" for e in info["cells"] if e["empty"]]
    touch = [f"{e['r']},{e['c']}({''.join(e['touch'])})" for e in full if e["touch"]]
    L = [f"### {name}",
         f"- 크기 {W}×{H} · 격자 {rows}행 × {cols}열 ({src_of_grid}) · 칸 {cw:g}×{ch:g}px" + (f" · {fps}fps" if fps else ""),
         f"- 정수 칸: {'예' if cw == int(cw) and ch == int(ch) else '**아니오 — 칸 경계가 소수, 격자 확인 필요**'}"]
    if not full:
        L.append("- **모든 칸이 비어 있음**")
        return L
    L.append(f"- 빈 칸: {', '.join(empty) if empty else '없음'}")
    L.append(f"- 칸 가장자리에 닿은 칸 (잘렸을 수 있음): {', '.join(touch) if touch else '없음'}")
    L.append(f"- 최소 투명 여백 {min(e['margin'] for e in full)}px · 옅은 잡티(알파 1~{NOISE - 1}) {info['noise']}px")
    L.append("")
    L.append("| 행 | 몸 키(px) | 몸 폭 | 발 위치(칸 아래서) | 중심 x 흔들림 | 중심 y 흔들림 | 발 흔들림 |")
    L.append("|---|---|---|---|---|---|---|")
    for r in range(rows):
        rc = [e for e in full if e["r"] == r]
        if not rc:
            L.append(f"| {r} | - | - | - | - | - | - |")
            continue
        hs, ws, fs = [e["height"] for e in rc], [e["width"] for e in rc], [e["foot"] for e in rc]
        L.append(f"| {r} | {min(hs)}~{max(hs)} | {min(ws)}~{max(ws)} | {statistics.median(fs):g} ({min(fs)}~{max(fs)}) "
                 f"| {spread([e['cx'] for e in rc]):.0f} | {spread([e['cy'] for e in rc]):.0f} | {spread(fs)} |")
    L.append(f"- 미리보기: `{info['preview']}`")
    return L


SKIP_DIRS = {"frames", "previews", "__macosx", "run"}


def main():
    try:
        import sys
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    ap = argparse.ArgumentParser()
    ap.add_argument("src")
    ap.add_argument("work")
    ap.add_argument("--rows", type=int)
    ap.add_argument("--cols", type=int)
    ap.add_argument("--sheet", action="append", help="이 시트만 검사 (여러 번 가능)")
    args = ap.parse_args()

    os.makedirs(args.work, exist_ok=True)
    root = extract(args.src, args.work)
    manifests = find_manifests(root)
    pngs = []
    frame_sizes = set()
    for d, _, files in os.walk(root):
        parts = {x.lower() for x in os.path.relpath(d, root).replace("\\", "/").split("/")}
        if parts & SKIP_DIRS:
            # 개별 프레임 폴더: 시트로 검사하지 않고 칸 크기 후보로만 사용
            for f in files[:3]:
                if f.lower().endswith(".png"):
                    with Image.open(os.path.join(d, f)) as fi:
                        frame_sizes.add(fi.size)
            continue
        pngs += [os.path.join(d, f) for f in files if f.lower().endswith(".png")]
    if args.sheet:
        pngs = [p for p in pngs if os.path.basename(p) in args.sheet]
    frames_dirs = sum(1 for d, _, _ in os.walk(root) if os.path.basename(d).lower() == "frames")

    out = [f"# 스프라이트 묶음 검사: {os.path.basename(args.src)}", "",
           f"- 풀린 위치: `{root}`",
           f"- manifest.json {len(manifests)}개 · 시트 {len(pngs)}장 · 프레임 폴더 {frames_dirs}개 (개별 프레임은 검사 생략)"]
    for d, m in manifests:
        if isinstance(m, dict) and m.get("game_input"):
            out.append(f"- 게임용 시트 지정 (game_input): `{m['game_input']}`")
        keys = ", ".join(list(m.keys())[:12])
        out.append(f"- manifest `{os.path.relpath(os.path.join(d, 'manifest.json'), root)}`: {keys}")
    for d, _, files in os.walk(root):
        for f in files:
            if f.lower().endswith(".txt"):
                out.append(f"- 안내 문서: `{os.path.relpath(os.path.join(d, f), root)}` (직접 읽어볼 것)")
    out.append("")

    for p in sorted(pngs):
        name = os.path.basename(p)
        rows = cols = fps = None
        src_of_grid = "추정"
        for _, m in manifests:
            r, c, f = grid_from_manifest(m, name)
            if r and c:
                rows, cols, fps, src_of_grid = int(r), int(c), f, "manifest"
                break
        if args.rows and args.cols:
            rows, cols, src_of_grid = args.rows, args.cols, "지정"
        if not rows:
            with Image.open(p) as im:
                W, H = im.size
            cands = []
            for _, m in manifests:
                cell_sizes(m, cands)
            cands += sorted(frame_sizes)
            for cw, ch in cands:
                if cw and ch and W % cw == 0 and H % ch == 0 and (W // cw) * (H // ch) > 1:
                    rows, cols, src_of_grid = H // ch, W // cw, f"manifest/프레임 칸 {cw}×{ch}"
                    break
        if not rows:
            rows, cols = guess_grid(W, H)
        info = inspect_sheet(p, rows, cols, os.path.join(args.work, "previews"))
        out += report_sheet(name, info, rows, cols, fps, src_of_grid) + [""]

    rp = os.path.join(args.work, "report.md")
    with open(rp, "w", encoding="utf-8") as fp:
        fp.write("\n".join(out))
    print("\n".join(out))
    print(f"\n[report] {rp}")


if __name__ == "__main__":
    main()
