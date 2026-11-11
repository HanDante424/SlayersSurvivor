"""캐릭터 스프라이트 묶음(스탠딩 + 이동 + 동작들)을 게임용 공통 규격으로 정렬.

usage:
  python -I Tools/normalize_character_sheets.py <묶음 폴더> <출력 폴더> [--cell 512] [--height 400] [--baseline 32] [--engine-out <폴더>]

  --engine-out: 게임용 시트를 SpriteSheetAnimator 규칙(행 = 아래·왼쪽·오른쪽·위, 열 = 프레임)으로 추가 저장
                <이름>_Idle.png (4행 × 1열, 스탠딩) · <이름>_Run.png (4행 × N열, 이동) · <이름>_<동작>.png (공격 등)
                → Assets/Resources/Sprites 에 넣어 사용

입력 manifest.json 형식 (둘 다 지원)
  A) {"character", "standing": {sheet, frames: {방향: {rect}}}, "move": {sheet, rows, cell_size, fps, frames: {방향: [{rect}]}}}
  B) {"character", "actions": {"Standing": {sheet, cell_size, directions, layout "4 columns x 1 row"},
                                "Run": {sheet, cell_size, directions, frames_per_direction, fps}, "<동작>": {...}}}
     B 에서 Standing · Run 외 동작(예: Slash)은 추가 동작으로 함께 정렬

출력 (그림 픽셀은 새로 그리지 않고 크기 조절·위치 이동만):
  sheets/ · frames/ · manifest.json · previews/compare.png · previews/<동작>_<방향>.gif

규격:
  - 모든 동작이 같은 칸 크기 (cell)
  - 캐릭터 키 = height px (스탠딩은 최대 키, 이동은 방향별 최대 키의 중앙값 기준)
  - 이동: 프레임마다 발끝(불투명 영역 맨 아래)을 칸 아래 baseline px 위에 맞춤 (달리기 상하 흔들림 제거)
  - 추가 동작(공격 등): 첫 프레임(준비 자세)의 **몸 높이**(검처럼 얇은 부분 제외)가 height 가 되도록 별도 배율
    (원본 시트마다 캐릭터를 그린 크기가 달라서). 방향마다 **첫 프레임(준비 자세)의 발끝·중심**을
    기준으로 그 방향 모든 프레임을 같은 만큼 옮김 → 무기가 발 아래로 내려가도 몸이 뜨지 않고 원래 동작이 유지됨
  - 가로: 스탠딩은 캐릭터 중심을 칸 중앙에, 이동은 원본 칸 안 좌우 흔들림을 배율만큼 유지
"""
import argparse
import json
import os
import statistics

from PIL import Image

SOLID = 40      # 발·키 측정용 불투명 기준 (빛 번짐 제외)
KEEP = 4        # 잘라낼 때 남길 최소 알파 (빛 번짐 포함)
DIRS = ["Down", "Left", "Right", "Up"]


def solid_bbox(im):
    return im.getchannel("A").point(lambda v: 255 if v > SOLID else 0).getbbox()


def keep_bbox(im):
    return im.getchannel("A").point(lambda v: 255 if v > KEEP else 0).getbbox()


def body_height(im, min_row_px):
    """무기처럼 얇은 부분을 빼고 잰 몸 높이: 불투명 픽셀이 min_row_px 이상인 행의 위~아래"""
    a = im.getchannel("A").point(lambda v: 255 if v > SOLID else 0)
    w, h = a.size
    px = a.load()
    ys = [y for y in range(h) if sum(1 for x in range(w) if px[x, y]) >= min_row_px]
    return (ys[-1] - ys[0]) if ys else None


def crop_rect(sheet, rect):
    x, y, w, h = rect
    return sheet.crop((x, y, x + w, y + h))


def place_mapped(cell_img, src, scale, anchor_x, anchor_y, out_x, out_y):
    """src 칸 좌표 (anchor_x, anchor_y) 가 출력 칸 (out_x, out_y) 에 오도록 배율 조정해 붙임. 칸 밖으로 나가면 잘림 → True"""
    kb = keep_bbox(src)
    if kb is None:
        return False
    part = src.crop(kb)
    nw, nh = max(1, round(part.width * scale)), max(1, round(part.height * scale))
    part = part.resize((nw, nh), Image.LANCZOS)
    x = round(out_x + (kb[0] - anchor_x) * scale)
    y = round(out_y + (kb[1] - anchor_y) * scale)
    W, H = cell_img.size
    # 칸 밖으로 나가는 부분은 잘라서 합성 (paste+mask 는 반투명 알파가 제곱돼 번짐이 어두워지므로 alpha_composite 사용)
    sx, sy = max(0, -x), max(0, -y)
    ex, ey = min(nw, W - x), min(nh, H - y)
    if ex > sx and ey > sy:
        cell_img.alpha_composite(part, dest=(x + sx, y + sy), source=(sx, sy, ex, ey))
    return x < 0 or y < 0 or x + nw > W or y + nh > H


def place_by_feet(cell_img, src, scale, cell, base, center_x):
    """src 의 불투명 영역 발끝 · 가로 중심을 (center_x, cell - base) 에 맞춤"""
    sb = solid_bbox(src)
    if sb is None:
        return False
    return place_mapped(cell_img, src, scale, (sb[0] + sb[2]) / 2, sb[3], center_x, cell - base)


# ---------- 입력 읽기 ----------
def load_bundle(path):
    """두 manifest 형식을 공통 구조로: name, standing{dir: img}, actions{name: {"frames": {dir: [img]}, "cell_w", "fps"}}"""
    m = json.load(open(os.path.join(path, "manifest.json"), encoding="utf-8-sig"))
    name = m.get("character", "Character")

    def sheet(rel):
        return Image.open(os.path.join(path, rel)).convert("RGBA")

    actions = {}
    if "actions" in m:   # 형식 B
        acts = m["actions"]
        st = acts["Standing"]
        sh = sheet(st["sheet"])
        cw, ch = st["cell_size"]
        dirs = st.get("directions", DIRS)
        if "row" in st.get("layout", "") and not st.get("layout", "").startswith("4 columns"):
            standing = {d: sh.crop((0, i * ch, cw, (i + 1) * ch)) for i, d in enumerate(dirs)}
        else:   # 4 columns x 1 row
            standing = {d: sh.crop((i * cw, 0, (i + 1) * cw, ch)) for i, d in enumerate(dirs)}
        for key, a in acts.items():
            if key == "Standing":
                continue
            sh = sheet(a["sheet"])
            cw, ch = a["cell_size"]
            n = a["frames_per_direction"]
            frames = {d: [sh.crop((k * cw, r * ch, (k + 1) * cw, (r + 1) * ch)) for k in range(n)]
                      for r, d in enumerate(a.get("directions", DIRS))}
            actions[key] = {"frames": frames, "cell_w": cw, "fps": a.get("fps") or 10}
        move_key = "Run" if "Run" in actions else next(iter(actions))
    else:                # 형식 A
        st, mv = m["standing"], m["move"]
        dirs = mv["rows"]
        st_sheet, mv_sheet = sheet(st["sheet"]), sheet(mv["sheet"])
        standing = {d: crop_rect(st_sheet, st["frames"][d]["rect"]) for d in dirs}
        actions["Run"] = {"frames": {d: [crop_rect(mv_sheet, f["rect"]) for f in mv["frames"][d]] for d in dirs},
                          "cell_w": mv["cell_size"][0], "fps": mv.get("fps", 10)}
        move_key = "Run"
    return name, dirs, standing, actions, move_key


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("bundle")
    ap.add_argument("out")
    ap.add_argument("--cell", type=int, default=512)
    ap.add_argument("--height", type=int, default=400)
    ap.add_argument("--baseline", type=int, default=32)
    ap.add_argument("--engine-out", default=None)
    ap.add_argument("--action-cell", type=int, default=None,
                    help="추가 동작(공격 등)만 더 큰 칸으로 (무기가 칸 밖으로 나갈 때). 배율 · 발 기준선 px 는 같음")
    a = ap.parse_args()

    name, dirs, st_src, actions, move_key = load_bundle(a.bundle)
    cell, H, base = a.cell, a.height, a.baseline
    mv = actions[move_key]

    # ---- 배율: 스탠딩 · 이동 캐릭터 키를 H 로. 추가 동작은 이동과 같은 원본 칸 규격이면 이동 배율 ----
    st_h = max(solid_bbox(im)[3] - solid_bbox(im)[1] for im in st_src.values())
    mv_dir_max = [max(solid_bbox(im)[3] - solid_bbox(im)[1] for im in mv["frames"][d]) for d in dirs]
    mv_h = statistics.median(mv_dir_max)
    s_st, s_mv = H / st_h, H / mv_h
    print(f"standing height {st_h}px -> scale {s_st:.4f}")
    print(f"{move_key} height (per-dir max {mv_dir_max}, median {mv_h}) -> scale {s_mv:.4f}")

    os.makedirs(os.path.join(a.out, "sheets"), exist_ok=True)
    os.makedirs(os.path.join(a.out, "previews"), exist_ok=True)
    clipped = []

    def save_frame(img, *parts):
        p = os.path.join(a.out, "frames", *parts[:-1])
        os.makedirs(p, exist_ok=True)
        img.save(os.path.join(p, parts[-1]))

    # ---- 스탠딩 ----
    st_cells = {}
    for d in dirs:
        c = Image.new("RGBA", (cell, cell), (0, 0, 0, 0))
        if place_by_feet(c, st_src[d], s_st, cell, base, cell / 2):
            clipped.append(f"Standing/{d}")
        st_cells[d] = c
        save_frame(c, "Standing", f"{d}.png")
    st_out = Image.new("RGBA", (cell * len(dirs), cell), (0, 0, 0, 0))
    for i, d in enumerate(dirs):
        st_out.alpha_composite(st_cells[d], (i * cell, 0))
    st_out.save(os.path.join(a.out, "sheets", f"{name}_Standing.png"))

    # ---- 동작들 ----
    act_cells = {}
    for key, act in actions.items():
        frames = act["frames"]
        n = len(frames[dirs[0]])
        ac = cell if key == move_key or not a.action_cell else a.action_cell   # 이 동작의 출력 칸 크기
        act["cell_out"] = ac
        src_w = act["cell_w"]
        if key == move_key:
            scale = s_mv
        else:
            thr = max(4, round(src_w * 0.045))   # 443px 칸 기준 약 20px 보다 얇은 행 = 무기
            bh = statistics.median([body_height(frames[d][0], thr) for d in dirs])
            scale = H / bh
            print(f"{key} ready-pose body height (median of first frames, weapon excluded) {bh}px -> scale {scale:.4f}")
        act["scale"] = scale
        act_cells[key] = {}
        for d in dirs:
            act_cells[key][d] = []
            anchor = solid_bbox(frames[d][0])   # 추가 동작: 그 방향 첫 프레임 기준
            for k, src in enumerate(frames[d]):
                c = Image.new("RGBA", (ac, ac), (0, 0, 0, 0))
                if key == move_key:
                    sb = solid_bbox(src)
                    off = ((sb[0] + sb[2]) / 2 - src_w / 2) * scale   # 원본 칸 중앙 기준 좌우 흔들림 유지
                    bad = place_by_feet(c, src, scale, cell, base, cell / 2 + off)
                else:
                    ax = (anchor[0] + anchor[2]) / 2
                    bad = place_mapped(c, src, scale, ax, anchor[3],
                                       ac / 2 + (ax - src_w / 2) * scale, ac - base)
                if bad:
                    clipped.append(f"{key}/{d}/{k:03d}")
                act_cells[key][d].append(c)
                save_frame(c, key, d, f"{k:03d}.png")
        sheet = Image.new("RGBA", (ac * n, ac * len(dirs)), (0, 0, 0, 0))
        for r, d in enumerate(dirs):
            for k, c in enumerate(act_cells[key][d]):
                sheet.alpha_composite(c, (k * ac, r * ac))
        sheet.save(os.path.join(a.out, "sheets", f"{name}_{key}.png"))
        act["sheet_out"] = sheet

    # ---- manifest ----
    json.dump({
        "character": name,
        "normalized_from": os.path.basename(os.path.normpath(a.bundle)),
        "cell_size": [cell, cell],
        "character_height_px": H,
        "foot_baseline_from_bottom_px": base,
        "pivot_normalized": [0.5, base / cell],
        "scale_applied": {"standing": round(s_st, 5), **{k: round(v["scale"], 5) for k, v in actions.items()}},
        "direction_order": dirs,
        "standing": {"sheet": f"sheets/{name}_Standing.png", "grid": [len(dirs), 1]},
        "actions": {k: {"sheet": f"sheets/{name}_{k}.png", "grid": [len(v["frames"][dirs[0]]), len(dirs)],
                        "cell_size": [v["cell_out"], v["cell_out"]], "pivot_normalized": [0.5, base / v["cell_out"]],
                        "fps": v["fps"], "aligned_by": "per-frame feet" if k == move_key else "first-frame anchor"}
                    for k, v in actions.items()},
        "clipped_frames": clipped,
    }, open(os.path.join(a.out, "manifest.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)

    # ---- 미리보기 ----
    pv = 0.35
    pc = round(cell * pv)
    rows = [(f"{k}", act_cells[k]) for k in actions]
    n_max = max(len(v[dirs[0]]) for _, v in rows)
    img = Image.new("RGBA", (pc * (n_max + 1) + 8, pc * len(dirs) * len(rows)), (40, 40, 48, 255))
    y0 = 0
    for key, cells in rows:
        for r, d in enumerate(dirs):
            seq = [st_cells[d]] + cells[d]
            for k, c in enumerate(seq):
                if c.width != cell:   # 큰 칸: 같은 배율로 보이게 아래 가운데 cell 크기만큼 잘라서 표시
                    o = (c.width - cell) // 2
                    c = c.crop((o, c.height - cell, o + cell, c.height))
                img.alpha_composite(c.resize((pc, pc), Image.LANCZOS), (k * pc + (8 if k else 0), y0 + r * pc))
            y = y0 + r * pc + round((cell - base) * pv)
            for x in range(img.width):
                img.putpixel((x, y), (255, 80, 80, 255))
        y0 += pc * len(dirs)
    img.save(os.path.join(a.out, "previews", "compare.png"))

    for key, act in actions.items():
        fps = act["fps"]
        for d in dirs:
            frames = []
            for c in act_cells[key][d]:
                cs = c.width
                bg = Image.new("RGBA", (cs, cs), (60, 60, 70, 255))
                bg.alpha_composite(c)
                for x in range(cs):
                    bg.putpixel((x, cs - base), (255, 80, 80, 255))
                frames.append(bg.convert("RGB").resize((cs // 2, cs // 2)))
            frames[0].save(os.path.join(a.out, "previews", f"{key}_{d}.gif"), save_all=True,
                           append_images=frames[1:], duration=round(1000 / fps), loop=0)

    # ---- 게임용 (SpriteSheetAnimator 규칙: 행 = 방향) ----
    if a.engine_out:
        os.makedirs(a.engine_out, exist_ok=True)
        idle = Image.new("RGBA", (cell, cell * len(dirs)), (0, 0, 0, 0))
        for r, d in enumerate(dirs):
            idle.alpha_composite(st_cells[d], (0, r * cell))
        idle.save(os.path.join(a.engine_out, f"{name}_Idle.png"))
        outs = [f"{name}_Idle {idle.size}"]
        for key, act in actions.items():
            act["sheet_out"].save(os.path.join(a.engine_out, f"{name}_{key}.png"))
            outs.append(f"{name}_{key} {act['sheet_out'].size}")
        print("engine sheets ->", a.engine_out, "(" + ", ".join(outs) + ")")

    print("clipped:", clipped or "none")
    print("done ->", a.out)


if __name__ == "__main__":
    main()
