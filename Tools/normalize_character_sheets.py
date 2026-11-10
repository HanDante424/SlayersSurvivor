"""캐릭터 스프라이트 묶음(스탠딩 + 이동)을 게임용 공통 규격으로 정렬.

usage:
  python -I Tools/normalize_character_sheets.py <묶음 폴더> <출력 폴더> [--cell 512] [--height 400] [--baseline 32] [--engine-out <폴더>]

  --engine-out: 게임용 시트를 SpriteSheetAnimator 규칙(행 = 아래·왼쪽·오른쪽·위, 열 = 프레임)으로 추가 저장
                <이름>_Idle.png (4행 × 1열, 스탠딩) · <이름>_Run.png (4행 × N열, 이동) → Assets/Resources/Sprites 에 넣어 사용

입력: sprite-tools 묶음 폴더 (manifest.json 에 standing / move 시트·칸 좌표·방향 순서가 있는 형식)
출력 (그림 픽셀은 새로 그리지 않고 크기 조절·위치 이동만):
  sheets/<이름>_Standing.png  4방향 × 1프레임, 칸 cell × cell
  sheets/<이름>_Move.png      방향별 1행 × N프레임, 칸 cell × cell
  frames/...                  프레임별 PNG
  manifest.json               새 칸 크기 · 기준선 · 배율 기록
  previews/compare.png        방향별 [스탠딩 | 이동 N프레임] + 발 기준선 (확인용)
  previews/Move_<방향>.gif    정렬 후 이동 미리보기

규격:
  - 스탠딩과 이동 모두 같은 칸 크기 (cell)
  - 캐릭터 키 = height px (스탠딩은 최대 키 기준, 이동은 방향별 최대 키의 중앙값 기준 — 다리를 굽힌 프레임 때문에 키가 들쭉날쭉하므로)
  - 발끝(불투명 영역 맨 아래) = 칸 아래에서 baseline px 위 → 프레임마다 발 높이 통일
  - 가로: 스탠딩은 캐릭터 중심을 칸 중앙에, 이동은 원본 칸 안에서의 좌우 흔들림을 배율만큼 유지
"""
import argparse
import json
import os
import statistics

from PIL import Image

SOLID = 40      # 발·키 측정용 불투명 기준 (빛 번짐 제외)
KEEP = 4        # 잘라낼 때 남길 최소 알파 (빛 번짐 포함)


def solid_bbox(im):
    return im.getchannel("A").point(lambda v: 255 if v > SOLID else 0).getbbox()


def keep_bbox(im):
    return im.getchannel("A").point(lambda v: 255 if v > KEEP else 0).getbbox()


def crop_rect(sheet, rect):
    x, y, w, h = rect
    return sheet.crop((x, y, x + w, y + h))


def place(cell_img, src, scale, cell, baseline, center_x=None):
    """src(원본 칸) → 배율 조정 후 cell 칸에 발끝을 기준선에 맞춰 붙임. center_x = 캐릭터 중심의 목표 x"""
    sb = solid_bbox(src)
    kb = keep_bbox(src)
    if sb is None or kb is None:
        return None
    part = src.crop(kb)
    nw, nh = max(1, round(part.width * scale)), max(1, round(part.height * scale))
    part = part.resize((nw, nh), Image.LANCZOS)
    # 불투명 영역(발끝·중심)의 위치를 잘라낸 조각 기준으로 환산
    foot_in_part = (sb[3] - kb[1]) * scale
    mid_in_part = ((sb[0] + sb[2]) / 2 - kb[0]) * scale
    if center_x is None:
        center_x = cell / 2
    x = round(center_x - mid_in_part)
    y = round((cell - baseline) - foot_in_part)
    cell_img.alpha_composite(part, (x, y)) if 0 <= x and 0 <= y and x + nw <= cell and y + nh <= cell else _paste_clip(cell_img, part, x, y)
    return (x, y, nw, nh)


def _paste_clip(dst, part, x, y):
    # 칸 밖으로 나가는 부분은 잘라서 붙임 (경고는 호출한 쪽에서)
    tmp = Image.new("RGBA", dst.size, (0, 0, 0, 0))
    tmp.paste(part, (x, y), part)
    dst.alpha_composite(tmp)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("bundle")
    ap.add_argument("out")
    ap.add_argument("--cell", type=int, default=512)
    ap.add_argument("--height", type=int, default=400)
    ap.add_argument("--baseline", type=int, default=32)
    ap.add_argument("--engine-out", default=None)
    a = ap.parse_args()

    m = json.load(open(os.path.join(a.bundle, "manifest.json"), encoding="utf-8"))
    name = m.get("character", "Character")
    st, mv = m["standing"], m["move"]
    dirs = mv["rows"]
    cell, H, base = a.cell, a.height, a.baseline

    st_sheet = Image.open(os.path.join(a.bundle, st["sheet"])).convert("RGBA")
    mv_sheet = Image.open(os.path.join(a.bundle, mv["sheet"])).convert("RGBA")

    st_src = {d: crop_rect(st_sheet, st["frames"][d]["rect"]) for d in dirs}
    mv_src = {d: [crop_rect(mv_sheet, f["rect"]) for f in mv["frames"][d]] for d in dirs}

    # ---- 배율: 두 시트의 캐릭터 키를 H 로 ----
    st_h = max(solid_bbox(im)[3] - solid_bbox(im)[1] for im in st_src.values())
    mv_dir_max = [max(solid_bbox(im)[3] - solid_bbox(im)[1] for im in mv_src[d]) for d in dirs]
    mv_h = statistics.median(mv_dir_max)
    s_st, s_mv = H / st_h, H / mv_h
    print(f"standing height {st_h}px -> scale {s_st:.4f}")
    print(f"move height (per-dir max {mv_dir_max}, median {mv_h}) -> scale {s_mv:.4f}")

    os.makedirs(os.path.join(a.out, "sheets"), exist_ok=True)
    os.makedirs(os.path.join(a.out, "previews"), exist_ok=True)
    report = {"clipped": []}

    # ---- 스탠딩 ----
    st_out = Image.new("RGBA", (cell * len(dirs), cell), (0, 0, 0, 0))
    st_cells = {}
    for i, d in enumerate(dirs):
        c = Image.new("RGBA", (cell, cell), (0, 0, 0, 0))
        r = place(c, st_src[d], s_st, cell, base)
        if r and (r[0] < 0 or r[1] < 0 or r[0] + r[2] > cell or r[1] + r[3] > cell):
            report["clipped"].append(f"Standing/{d}")
        st_cells[d] = c
        st_out.alpha_composite(c, (i * cell, 0))
        p = os.path.join(a.out, "frames", "Standing")
        os.makedirs(p, exist_ok=True)
        c.save(os.path.join(p, f"{d}.png"))
    st_out.save(os.path.join(a.out, "sheets", f"{name}_Standing.png"))

    # ---- 이동 ----
    n = len(mv_src[dirs[0]])
    mv_out = Image.new("RGBA", (cell * n, cell * len(dirs)), (0, 0, 0, 0))
    mv_cells = {}
    src_cell_w = mv["cell_size"][0]
    for row, d in enumerate(dirs):
        mv_cells[d] = []
        for k, src in enumerate(mv_src[d]):
            sb = solid_bbox(src)
            # 원본 칸 중앙에서의 좌우 위치를 배율만큼 유지 (달리기 흔들림 보존)
            off = ((sb[0] + sb[2]) / 2 - src_cell_w / 2) * s_mv
            c = Image.new("RGBA", (cell, cell), (0, 0, 0, 0))
            r = place(c, src, s_mv, cell, base, center_x=cell / 2 + off)
            if r and (r[0] < 0 or r[1] < 0 or r[0] + r[2] > cell or r[1] + r[3] > cell):
                report["clipped"].append(f"Move/{d}/{k:03d}")
            mv_cells[d].append(c)
            mv_out.alpha_composite(c, (k * cell, row * cell))
            p = os.path.join(a.out, "frames", "Move", d)
            os.makedirs(p, exist_ok=True)
            c.save(os.path.join(p, f"{k:03d}.png"))
    mv_out.save(os.path.join(a.out, "sheets", f"{name}_Move.png"))

    # ---- manifest ----
    out_m = {
        "character": name,
        "normalized_from": os.path.basename(os.path.normpath(a.bundle)),
        "cell_size": [cell, cell],
        "character_height_px": H,
        "foot_baseline_from_bottom_px": base,
        "pivot_normalized": [0.5, base / cell],
        "scale_applied": {"standing": round(s_st, 5), "move": round(s_mv, 5)},
        "direction_order": dirs,
        "standing": {"sheet": f"sheets/{name}_Standing.png", "grid": [len(dirs), 1]},
        "move": {"sheet": f"sheets/{name}_Move.png", "grid": [n, len(dirs)], "fps": mv.get("fps", 10), "loop": mv.get("loop", True)},
        "clipped_frames": report["clipped"],
    }
    json.dump(out_m, open(os.path.join(a.out, "manifest.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)

    # ---- 미리보기: 방향별 [스탠딩 | 이동 n] + 기준선, 축소 ----
    pv_scale = 0.35
    pc = round(cell * pv_scale)
    sheet = Image.new("RGBA", (pc * (n + 1) + 8, pc * len(dirs)), (40, 40, 48, 255))
    for row, d in enumerate(dirs):
        for k, c in enumerate([st_cells[d]] + mv_cells[d]):
            im = c.resize((pc, pc), Image.LANCZOS)
            x = k * pc + (8 if k > 0 else 0)
            sheet.alpha_composite(im, (x, row * pc))
        y = row * pc + round((cell - base) * pv_scale)
        for x in range(sheet.width):
            sheet.putpixel((x, y), (255, 80, 80, 255))
    sheet.save(os.path.join(a.out, "previews", "compare.png"))

    fps = mv.get("fps", 10)
    for d in dirs:
        frames = []
        for c in mv_cells[d]:
            bg = Image.new("RGBA", (cell, cell), (60, 60, 70, 255))
            bg.alpha_composite(c)
            for x in range(cell):
                bg.putpixel((x, cell - base), (255, 80, 80, 255))
            frames.append(bg.convert("RGB").resize((cell // 2, cell // 2)))
        frames[0].save(os.path.join(a.out, "previews", f"Move_{d}.gif"), save_all=True,
                       append_images=frames[1:], duration=round(1000 / fps), loop=0)

    # ---- 게임용 (SpriteSheetAnimator 규칙: 행 = 방향) ----
    if a.engine_out:
        os.makedirs(a.engine_out, exist_ok=True)
        idle = Image.new("RGBA", (cell, cell * len(dirs)), (0, 0, 0, 0))
        for row, d in enumerate(dirs):
            idle.alpha_composite(st_cells[d], (0, row * cell))
        idle.save(os.path.join(a.engine_out, f"{name}_Idle.png"))
        mv_out.save(os.path.join(a.engine_out, f"{name}_Run.png"))
        print(f"engine sheets -> {a.engine_out} ({name}_Idle {idle.size}, {name}_Run {mv_out.size})")

    print("clipped:", report["clipped"] or "none")
    print("done ->", a.out)


if __name__ == "__main__":
    main()
