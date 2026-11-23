// 밸런스 시뮬레이터 엔진 v2 — 게임의 피해 계산 (Assets/Scripts/Core/Damage.cs) 과 같은 순서
//   ① 공격자 단계: (초기 피해량 + 아이템 초기 피해량) × (1 + 피해량 % 합) + 추가 피해량 → × 최종 피해량 배율들 (스킬 · 패시브 · 아이템 · 전역, 곱연산)
//   ② 대상 단계 (맞은 적마다): × 조건부 최종 피해량 (화상 · 기절 상태) × 받는 최종 피해량 → 내림
//   고정 피해는 ① ② 의 증감을 모두 건너뜀
// 시간은 0.01초 단위. "주 대상" 1마리를 따라가며 상태 (화상 · 기절 · 받는 피해 증가 · 낙인) 를 추적하고,
// 광역 피해는 범위 안 적 수 N 을 곱해 합계에 더함 (범위 안 적은 모두 주 대상과 같은 상태라고 가정).
// 처치 효과 (살육의 열기 · 불길의 순환 · 죽음의 메아리 …) 는 "초당 처치 수" 로 일정하게 처치가 일어난다고 가정.

const MOD_PRODUCT = ['finalMul', 'cdMul', 'vsBurn', 'vsStun', 'intervalMul', 'delayMul'];
const MOD_SUM = ['dmgPct', 'stunAdd', 'countAdd', 'durAdd', 'durPct', 'hitCdRed', 'hitCdSec', 'burnAdd', 'stacksAdd'];

function mergeMods(list) {
  const m = { flags: {}, vuln: null, vulnBurning: null, extras: [], nullCd: null };
  for (const k of MOD_PRODUCT) m[k] = 1;
  for (const k of MOD_SUM) m[k] = 0;
  for (const x of list) {
    if (!x) continue;
    for (const k of MOD_PRODUCT) if (x[k] != null) m[k] *= x[k];
    for (const k of MOD_SUM) if (x[k] != null) m[k] += x[k];
    if (x.flags) Object.assign(m.flags, x.flags);
    if (x.vuln) m.vuln = x.vuln;
    if (x.vulnBurning) m.vulnBurning = x.vulnBurning;
    if (x.extras) m.extras = m.extras.concat(x.extras);
    if (x.extrasSet) m.extras = x.extrasSet.slice();   // 조합이 추가 사용을 통째로 바꿈
    if (x.nullCd) m.nullCd = x.nullCd;
  }
  if (m.flags.noExtras) m.extras = [];
  return m;
}

function paramValues(def, overrides) {
  const p = {};
  for (const k in def.params) p[k] = overrides && overrides[k] != null ? overrides[k] : def.params[k].v;
  if (overrides && overrides.cd != null) p.cd = overrides.cd;   // 스킬 기본 쿨타임 (params 목록에 없는 별도 칸)
  return p;
}

// 아이템 · 세트 효과 합치기 (곱: finalMul · cdMul, 합: 나머지)
const ITEM_SUM = ['dmgPct', 'initialAdd', 'flatAdd', 'burnPct', 'dotPct', 'stunPct', 'burnDps', 'burnStacks', 'burnDuration'];
const ITEM_PRODUCT = ['finalMul', 'cdMul'];
function itemTotals(items, sets) {
  const t = { count: 0 };
  for (const k of ITEM_SUM) t[k] = 0;
  for (const k of ITEM_PRODUCT) t[k] = 1;
  const apply = mods => {
    if (!mods) return;
    for (const k of ITEM_SUM) if (mods[k] != null) t[k] += mods[k];
    for (const k of ITEM_PRODUCT) if (mods[k] != null) t[k] *= mods[k];
  };
  const bySet = {};
  for (const it of items || []) {
    if (!it.on) continue;
    t.count++;
    apply(it.mods);
    if (it.set) bySet[it.set] = (bySet[it.set] || 0) + 1;
  }
  const activeSets = [];
  for (const s of sets || []) {
    if (!s.on) continue;
    if ((bySet[s.name] || 0) >= s.n) { apply(s.mods); activeSets.push(s.name + ' ' + s.n + '세트'); }
  }
  t.activeSets = activeSets;
  return t;
}

/**
 * 특수 강화 · 조합 1개의 실제 효과 = 정의의 mods + nums 에 화면에서 바꾼 값 (ov[id]['mods.경로'] · ['nums.키']) 을 덮어쓴 것
 * 반환: { mods, nums } — nums 는 { '특수강화id.키': 값 } (cast 안에서 NV(m, id, 키) 로 읽음)
 */
function specEff(sp, ov) {
  const o = (ov && ov[sp.id]) || {};
  const mods = JSON.parse(JSON.stringify(sp.mods || {}));
  for (const key in o) {
    if (key.indexOf('mods.') !== 0) continue;
    const path = key.slice(5).split('.');
    if (path[0] === 'extrasN' || path[0] === 'extrasSetN') {   // 추가 사용 횟수
      const which = path[0] === 'extrasN' ? 'extras' : 'extrasSet';
      const proto = (sp.mods && sp.mods[which] && sp.mods[which][0]) || {};
      mods[which] = Array.from({ length: Math.max(0, Math.round(o[key])) }, () => JSON.parse(JSON.stringify(proto)));
      continue;
    }
    let t = mods;
    for (let i = 0; i < path.length - 1 && t; i++) t = t[path[i]];
    if (t) t[path[path.length - 1]] = o[key];
  }
  const nums = {};
  for (const k in sp.nums || {}) nums[sp.id + '.' + k] = o['nums.' + k] != null ? o['nums.' + k] : sp.nums[k].v;
  return { mods, nums };
}

function comboAvailable(def, combo, sc) {
  const picks = Object.values(sc.picks || {});
  const any = combo.requiresAny || [combo.requires];
  return any.some(req => req.every(id => picks.includes(id)));
}

function runSim(cfg) {
  const G = cfg.G, DT = 0.01, EPS = 1e-4;
  const T = G.T;
  let now = 0;
  const IT = itemTotals(cfg.items, cfg.sets);
  // 패시브 특수 강화 수치: 정의 기본값 또는 화면에서 바꾼 값
  const PASS = {};
  for (const k in PASSIVES) for (const x of PASSIVES[k].specials) PASS[x.id] = x;
  // 기본 강화 1회당 증가량 (스킬 · 패시브별로 화면에서 바꾼 값 또는 기본값)
  const bvs = (id, k) => { const o = cfg.bv && cfg.bv['s:' + id]; return o && o[k] != null ? o[k] : BASIC_DEF.skill[k]; };
  const bvp = (k, n) => { const o = cfg.bv && cfg.bv['p:' + k]; return o && o[n] != null ? o[n] : BASIC_DEF[k][n]; };
  const pn = (id, k) => { const o = cfg.ov && cfg.ov[id] && cfg.ov[id]['nums.' + k]; return o != null ? o : PASS[id].nums[k].v; };
  const MIN = G.minDmg == null ? 1 : G.minDmg;   // 피해 1회 최소 피해 (게임 규칙: 0 보다 큰 피해는 최소 1)
  const fl = v => (v > 0 ? Math.max(MIN, Math.floor(v + EPS)) : 0);
  const BLAST = Math.max(1, Math.min(G.blastTargets, G.N));   // 적이 1마리면 폭발도 1마리만 맞힘

  // ───────── 패시브 설정 ─────────
  const FSC = cfg.pas.fs, HFC = cfg.pas.hf, ABC = cfg.pas.ab;
  const fsOn = !!FSC.on, hfOn = !!HFC.on, abOn = !!ABC.on;
  const fsHas = id => fsOn && (FSC.picks || []).includes(id);
  const hfHas = id => hfOn && (HFC.picks || []).includes(id);
  const abHas = id => abOn && (ABC.picks || []).includes(id);

  // ── 화염의 검 (화상)
  const fsP = paramValues({ params: PASSIVES.fs.params }, FSC.params);
  const fsUps = Object.assign({ dps: 0, stk: 0, dur: 0 }, FSC.ups);
  const durStat = (fsP.duration + bvp('fs', 'dur') * fsUps.dur + IT.burnDuration) * (fsHas('B5B') ? 1 + pn('B5B', 'dur') : 1);
  const burnDur = fsHas('B15B') ? Math.min(durStat, pn('B15B', 'dur')) : durStat;
  const burnPctSelf = (fsHas('B5B') ? pn('B5B', 'dmg') : 0) + (fsHas('B15B') ? Math.max(0, durStat - pn('B15B', 'dur')) * pn('B15B', 'perSec') : 0) + (fsHas('B15A') ? durStat * pn('B15A', 'perSec') : 0);
  const burnMax = fsP.maxStacks + bvp('fs', 'stk') * fsUps.stk + IT.burnStacks + (fsHas('B5C') ? pn('B5C', 'stacks') : 0) + (fsHas('B15A') ? pn('B15A', 'stacks') : 0);
  const burnDps = fsP.dps + bvp('fs', 'dps') * fsUps.dps + IT.burnDps;
  const eternal = fsHas('B15A');
  const meltPer = (fsHas('B5A') ? pn('B5A', 'per') : 0) + (G.melt || 0);

  // ── 옥염
  const hfUps = Object.assign({ fin: 0, mob: 0, dot: 0 }, HFC.ups);
  const hfBase = hfOn ? 1 + 0.30 + bvp('hf', 'fin') * hfUps.fin : 1;
  const hfCd = hfOn ? 1 - bvp('hf', 'mob') * hfUps.mob : 1;
  const hfDotPct = hfOn ? bvp('hf', 'dot') * hfUps.dot : 0;
  const slaughter = { n: 0, left: 0 };

  // ── 흡수 가열
  const abUps = Object.assign({ fin: 0, cd: 0, stk: 0 }, ABC.ups);
  const abMaxRaw = 10 + bvp('ab', 'stk') * abUps.stk;
  const abMax = abHas('H5C') ? Math.ceil(abMaxRaw * pn('H5C', 'halve')) : abMaxRaw;
  let abHold = 0, abCritical = false;

  // ───────── 주 대상 상태 ─────────
  const burn = { stacks: 0, expire: 0, next: 0, brand: 0, detReady: 0 };   // detReady: 일촉즉발 대상당 쿨타임이 끝나는 시각
  let stunUntil = 0;
  const vuln = {};   // key → {n, per, max}
  const isBurning = () => burn.stacks > 0;
  const isStunned = () => now < stunUntil;
  const burningCount = () => isBurning() ? Math.max(1, Math.round(G.N * G.burnShare)) : 0;
  const abStacks = () => abOn && isBurning() ? Math.min(abMax, burningCount()) : 0;
  const abEff = () => 1 + (abHas('H5C') ? pn('H5C', 'bonus') : 0) + (abHas('H10C') ? pn('H10C', 'bonus') : 0) + (abCritical ? pn('H15C', 'bonus') : 0);
  const abScaled = per => per * abStacks() * abEff();
  function received() {
    let r = 1 + meltPer * burn.stacks;
    for (const k in vuln) r *= 1 + vuln[k].per * vuln[k].n;
    return r;
  }

  // ───────── 전역 배율 ─────────
  /** 최종 피해량 배율 (고정 피해 제외): 아이템 × 옥염 × 흡수 가열 × 전역 */
  function globalFinal(isBurnTick) {
    let f = IT.finalMul * G.dmgMul;
    if (hfOn) {
      f *= hfBase;
      if (hfHas('Y5B')) f *= 1 + pn('Y5B', 'final');
      if (hfHas('Y10B')) f *= 1 + pn('Y10B', 'final');
      if (hfHas('Y15B')) f *= 1 + pn('Y15B', 'final');
      if (hfHas('Y5C')) f *= 1 + pn('Y5C', 'per') * slaughter.n;
      if (isBurnTick) { if (hfHas('Y5A')) f *= 1 + pn('Y5A', 'burn'); if (hfHas('Y10A')) f *= 1 + pn('Y10A', 'burn'); }
    }
    if (abOn) f *= 1 + (0.05 + bvp('ab', 'fin') * abUps.fin) * abStacks() * abEff();
    return f;
  }
  const globalDmgPct = () => G.dmgPct + IT.dmgPct;
  function dotPct() {
    let p = IT.dotPct + hfDotPct;
    if (abHas('H10A')) p += abScaled(pn('H10A', 'per'));
    return p;
  }
  /** 모든 지속 피해 간격 (가속 연소) */
  function dotIv(base) {
    const r = abHas('H15A') ? abScaled(pn('H15A', 'per')) : 0;
    return r > 0 ? Math.max(0.1, base - r) : base;
  }
  /** 쿨타임 배율 구성 요소 (줄이는 것 · 늘리는 것 모두) */
  function cdParts(sk) {
    const parts = [];
    parts.push(1 - Math.min(0.9, bvs(sk.id, 'cd') * sk.ups.cd));      // 기본 강화
    parts.push(sk.m.cdMul);                                 // 특수 강화
    parts.push(G.cdMul, IT.cdMul, hfCd);                    // 전역 · 아이템 · 옥염
    if (abOn) parts.push(Math.max(0, 1 - abScaled(0.01 + bvp('ab', 'cd') * abUps.cd)));
    return parts;
  }
  function cdMulOf(sk) {
    const nullify = sk.m.nullCd != null;
    let mul = 1;
    for (const f of cdParts(sk)) mul *= nullify && f < 1 ? 1 : f;
    return Math.max(0.1, mul);
  }
  /** 무효화할 쿨타임 감소율 (%): 줄이는 요소만 곱한 값 */
  function cdReductionPct(sk) {
    let mul = 1;
    for (const f of cdParts(sk)) if (f < 1) mul *= f;
    return Math.min(90, (1 - Math.max(0.1, mul)) * 100);
  }

  // ───────── 결과 ─────────
  const R = {
    total: 0, main: 0, ttk: null, perSkill: {}, passive: { total: 0, main: 0, ticks: 0, extra: {} },
    buckets: [], burnTime: 0, stunTime: 0, T, maxBurnStacks: 0, kills: 0, items: IT,
  };
  // 주 대상 누적 피해가 체력 (G.enemyHp 와 보스 프리셋 목록 cfg.hpList) 에 처음 닿은 시각
  const hpList = cfg.hpList || [];
  R.ttks = {};
  function markMain() {
    if (R.ttk == null && R.main >= G.enemyHp) R.ttk = now;
    for (const h of hpList) if (R.ttks[h] == null && R.main >= h) R.ttks[h] = now;
  }
  const nBuckets = Math.ceil(T);
  const bucketOf = t => Math.min(nBuckets - 1, Math.floor(t));
  function addBucket(id, v) {
    const b = bucketOf(now);
    if (!R.buckets[b]) R.buckets[b] = {};
    R.buckets[b][id] = (R.buckets[b][id] || 0) + v;
  }
  function addPassive(label, perTarget, nT) {
    const tot = perTarget * nT;
    R.passive.total += tot; R.passive.main += perTarget;
    R.total += tot; R.main += perTarget;
    const e = R.passive.extra[label] || (R.passive.extra[label] = { total: 0, hits: 0 });
    e.total += tot; e.hits++;
    markMain();
    addBucket('passive', tot);
  }
  function addDamage(sk, label, perTarget, nTargets, extra) {
    const tot = perTarget * nTargets;
    R.total += tot; R.main += perTarget;
    markMain();
    sk.total += tot; sk.main += perTarget;
    const b = sk.labels[label] || (sk.labels[label] = { total: 0, hits: 0, hitsBurn: 0, hitsStun: 0, condBonus: 0 });
    b.total += tot; b.hits += 1;
    if (extra) { if (extra.burning) b.hitsBurn++; if (extra.stunned) b.hitsStun++; b.condBonus += (extra.bonus || 0) * nTargets; }
    addBucket(sk.id, tot);
  }

  // ───────── 사건 큐 ─────────
  const queue = [];
  function schedule(t, fn) {
    let i = queue.length;
    while (i > 0 && queue[i - 1].t > t) i--;
    queue.splice(i, 0, { t, fn });
  }

  // ───────── 스킬 실행 상태 ─────────
  const runs = [];
  for (const sc of cfg.skills) {
    if (!sc.on) continue;
    const def = SKILLS.find(s => s.id === sc.id);
    const picked = Object.values(sc.picks || {}).map(id => def.specials.find(s => s.id === id)).filter(Boolean);
    const effs = picked.map(sp => specEff(sp, cfg.ov));
    const combo = sc.combo && def.combos ? def.combos.find(c => c.id === sc.combo) : null;
    const comboOn = !!(combo && comboAvailable(def, combo, sc));
    if (comboOn) effs.push(specEff(combo, cfg.ov));
    const m = mergeMods(effs.map(e => e.mods));
    m.nums = Object.assign({}, ...effs.map(e => e.nums));
    const sk = { id: def.id, name: def.name, def, m, p: paramValues(def, sc.params), ups: sc.ups || { dmg: 0, cd: 0 },
                 nextReady: 0, casts: 0, total: 0, main: 0, labels: {}, comboOn, stacks: 0, rc: 0, lock: 0,
                 pctSum: 0, finSum: 0, hitN: 0, _lp: 0, _lf: 1 };
    sk.maxStacks = m.flags.nullStacks ? 1 : Math.max(1, (def.stacks || 1) + m.stacksAdd);   // 스택 능력치 무효화 = 최대 스택 1
    sk.stackMode = sk.maxStacks > 1;
    sk.stacks = sk.maxStacks;
    runs.push(sk);
    R.perSkill[def.id] = sk;
  }

  function attackerAmount(sk, initial, o, isDot) {
    const pct = sk.m.dmgPct + sk.ups.dmg * bvs(sk.id, 'dmg') + (o.extraPct || 0) + globalDmgPct() + (isDot ? dotPct() : 0);
    const v = (initial + IT.initialAdd) * (1 + pct) + IT.flatAdd;
    let fin = sk.m.finalMul * (o.extraFinal || 1) * globalFinal(false);
    const nc = sk.m.nullCd;
    if (nc) fin *= 1 + (nc.base || 0) + (nc.perPct || 0) * cdReductionPct(sk) + (nc.perStack || 0) * (sk.m.flags.nullStacks ? Math.max(0, (sk.def.stacks || 1) + (sk.m.stacksAdd || 0) - 1) : (sk.m.stacksAdd || 0));   // 무효화한 스택 수 = 무효화 전 최대 스택 - 1
    sk._lp = pct; sk._lf = fin;   // 화면 표시용 (이번 피해에 적용된 피해량 % · 최종 피해량 배율)
    return v * fin;
  }

  const burnInterval = () => dotIv(fsP.interval);
  function addBurn(n) {
    if (!fsOn || n <= 0) return;
    if (burn.stacks === 0) burn.next = now + burnInterval();
    burn.stacks = Math.min(burnMax, burn.stacks + n);
    burn.expire = eternal ? Infinity : now + burnDur;
    if (burn.stacks > R.maxBurnStacks) R.maxBurnStacks = burn.stacks;
    if (fsHas('B10A') && burn.stacks >= burnMax && now + 1e-9 >= burn.detReady) {   // 일촉즉발: 최대 스택 도달 → 일괄 적용, 스택 소모 (대상당 쿨타임 안에는 다시 터지지 않음)
      burn.detReady = now + pn('B10A', 'icd');
      const tot = burnTotal();
      clearBurn();
      if (tot > 0) addPassive('일촉즉발', tot, G.N);
    }
  }
  function clearBurn() { burn.stacks = 0; burn.brand = 0; }
  /** 화상 1틱 (대상 단계 전): 스택 × 초당 피해 → 피해량 % → 최종 피해량 (화상 태그) */
  function burnTickRaw(stacks, brand) {
    const pct = burnPctSelf + (fsHas('B15C') ? pn('B15C', 'per') * (brand || 0) : 0) + IT.burnPct + G.burnPct
              + (abHas('H5A') ? abScaled(pn('H5A', 'per')) : 0) + dotPct() + globalDmgPct();
    return burnDps * stacks * (1 + pct) * globalFinal(true);
  }
  /** 강제 발화 · 일촉즉발 · 폭발이 참조하는 "전체 지속시간 동안의 총 화상 피해" = 틱 수 × 내림한 1틱 피해 */
  /** 지속 피해의 총 피해량 = 1틱 × 스택 (화상 1틱 피해에 스택이 이미 곱해짐) — 타오르는 검 · 연쇄 폭발 · 살아있는 폭탄이 사용 */
  function burnOneTick() {
    if (burn.stacks <= 0) return 0;
    return fl(burnTickRaw(burn.stacks, burn.brand));
  }
  function burnTotal() {
    if (burn.stacks <= 0) return 0;
    const full = eternal ? durStat : burnDur;
    const ticks = Math.floor(full / burnInterval() + 1e-4);
    return ticks * fl(burnTickRaw(burn.stacks, burn.brand));
  }
  function incinerate(nT) {
    if (!hfHas('Y15A') || !G.enemyHp) return;
    const pct = G.boss ? pn('Y15A', 'boss') : pn('Y15A', 'pct');
    addPassive('소각', Math.max(1, Math.floor(G.enemyHp * pct + EPS)), nT);
  }

  function reduceCdRatio(sk, ratio, times) {
    if (ratio <= 0) return;
    const f = Math.pow(1 - ratio, times || 1);
    // 한 번의 쿨타임 안에서 이 방식으로 줄일 수 있는 총량은 90% 까지 (스킬 쿨타임 최종 감소 상한과 같게)
    const hf = sk.hitFactor == null ? 1 : sk.hitFactor;
    const nf = Math.max(0.1, hf * f), eff = nf / hf;
    sk.hitFactor = nf;
    if (sk.stackMode) { if (sk.stacks < sk.maxStacks && sk.rc > 0) sk.rc *= eff; }
    else { const remain = sk.nextReady - now; if (remain > 0) sk.nextReady -= remain * (1 - eff); }
  }
  function cooldownOf(sk) {
    const base = sk.p.cd != null ? sk.p.cd : sk.def.cd;
    return base * cdMulOf(sk);
  }
  function reduceCdSec(sk, sec) {
    if (sec <= 0) return;
    if (sk.stackMode) {
      let s = sec;
      while (s > 0 && sk.stacks < sk.maxStacks) {
        if (sk.rc <= 0) sk.rc = cooldownOf(sk);
        if (sk.rc > s) { sk.rc -= s; return; }
        s -= sk.rc; sk.stacks++; sk.rc = sk.stacks < sk.maxStacks ? cooldownOf(sk) : 0;
      }
    } else sk.nextReady -= Math.min(sec, Math.max(0, sk.nextReady - now));
  }

  function addVuln(v) {
    const key = v.key || 'v';
    const e = vuln[key] || (vuln[key] = { n: 0, per: v.per, max: v.max });
    e.n = Math.min(e.max, e.n + 1);
  }
  /** 직접 피해 적중 후 효과: 화염의 검 화상 부여 · 살아있는 폭탄 · 화염 낙인 · 강제 발화 */
  function afterDirectHit(sk, o, wasBurning, nT, fromFixed) {
    if (!fsOn) return;
    if (!fromFixed && fsHas('B10B') && wasBurning) {   // 살아있는 폭탄: 화상 상태 적 직접 피해 → 폭발 (화상 총 피해량 = 1틱 × 스택 의 100% 고정)
      const tot = burnOneTick();
      if (tot > 0) addPassive('살아있는 폭탄', tot * pn('B10B', 'ratio'), BLAST);
    }
    if (!fromFixed && fsHas('B15C') && wasBurning) burn.brand = Math.min(pn('B15C', 'max'), burn.brand + 1);   // 화염 낙인
    if (o.burn !== false && !fromFixed) addBurn(1 + (o.burnAdd || 0) + (sk.m.burnAdd || 0));
    if (!fromFixed && sk.m.flags.forceBurn && burn.stacks > 0) {   // 강제 발화: 적중 시 부여된 모든 화상 일괄 적용
      const tot = burnTotal();
      clearBurn();
      if (tot > 0) addPassive('강제 발화', tot, nT);
    }
  }

  function makeCtx(sk, u) {
    const ratioN = single => single ? 1 : (u.alloc != null ? u.alloc : Math.max(1, Math.round(G.N * (u.ratio == null ? 1 : u.ratio))));
    const c = {
      G, u, now: () => now, blast: BLAST, killBlast: Math.max(0, Math.min(G.blastTargets, G.N - 1)),   // 처치 효과가 맞히는 적 수: 죽은 적 주변에 남은 적 (죽은 적은 제외 → 적이 1마리면 0)
        // 폭발 하나가 맞히는 적 수 (범위 안 적 수 이하)
      at: (t, fn) => schedule(now + (u.delay || 0) + t, fn),
      A: (initial, o) => Math.floor(attackerAmount(sk, initial, o || {}, false) + EPS),
      burning: isBurning, burnStacks: () => burn.stacks, burnTotal, burnTick: burnOneTick,
      reduceCdRatio: (r, times) => reduceCdRatio(sk, r, times),
      reduceCdSec: s => reduceCdSec(sk, s),
      dotIv,
      burnBoost: n => { if (isBurning()) addBurn(n); },
      stunOf: s => s * (1 + IT.stunPct),
      hit(initial, o) {
        o = o || {};
        if (u.alloc === 0) return 0;   // 이 사용은 맞힐 적이 없음 (적이 사용 수보다 적을 때)
        const nT = o.hits != null ? o.hits : ratioN(!!o.single);
        const dot = !!o.dot;
        const amount = attackerAmount(sk, initial, { extraPct: o.extraPct, extraFinal: (o.extraFinal || 1) * (u.final || 1) }, dot);
        sk.pctSum += sk._lp; sk.finSum += sk._lf; sk.hitN++;
        const burning = isBurning(), stunned = isStunned();
        let mult = received();
        const dmgNoCond = Math.floor(amount * mult + EPS);
        if (burning) {
          mult *= sk.m.vsBurn;
          if (sk.m.flags.searing) mult *= 1 + Math.min(sk.m.nums['T10C.cap'], sk.m.nums['T10C.per'] * burn.stacks);   // 고열 절삭: 화상 스택당 +5% (상한 75%)
        }
        if (stunned) mult *= sk.m.vsStun;
        const dmg = fl(amount * mult);
        addDamage(sk, o.label || '피해', dmg, nT, { burning, stunned, bonus: dmg - dmgNoCond });
        if (dot) incinerate(nT); else afterDirectHit(sk, o, burning, nT, false);
        if (o.stun > 0) stunUntil = (isStunned() ? stunUntil : now) + c.stunOf(o.stun);
        const vb = o.vulnBurning || sk.m.vulnBurning;
        if (vb && burning && o.vulnBurning !== null) addVuln(vb);
        if (o.vuln) addVuln(o.vuln);
        if (sk.m.hitCdRed > 0) reduceCdRatio(sk, sk.m.hitCdRed, nT);
        if (sk.m.hitCdSec > 0) reduceCdSec(sk, sk.m.hitCdSec * nT);
        return dmg;
      },
      fixed(amount, o) {
        o = o || {};
        if (u.alloc === 0 || o.hits === 0) return 0;
        const nT = o.hits != null ? o.hits : ratioN(!!o.single);
        const dmg = fl(amount);
        const burningBefore = isBurning();
        addDamage(sk, o.label || '고정 피해', dmg, nT * (o.rate == null ? 1 : o.rate), null);   // rate = 명중 확률 (유도 없는 검기 등)
        if (o.dot) incinerate(nT);
        else if (o.burn !== false) afterDirectHit(sk, o, burningBefore, nT, false);   // 고정 피해도 직접 피해 → 화염의 검 화상 부여 (지속 피해는 제외)
        return dmg;
      },
    };
    return c;
  }

  // ───────── 처치 효과 ─────────
  function onKill() {
    R.kills++;
    const wasBurning = isBurning();
    if (hfHas('Y5C')) { slaughter.n = Math.min(pn('Y5C', 'max'), slaughter.n + 1); slaughter.left = pn('Y5C', 'time'); }
    if (hfHas('Y10C') && wasBurning)
      for (const sk of runs) reduceCdRatio(sk, pn('Y10C', 'ratio'));   // 불길의 순환
    if (fsHas('B5C') && wasBurning) addBurn(pn('B5C', 'give'));         // 확산되는 불길
    for (const sk of runs) if (sk.def.onKill) sk.def.onKill(makeCtx(sk, { ratio: 1 }), sk.p, sk.m);
  }
  const killIv = G.killRate > 0 ? 1 / G.killRate : Infinity;
  let nextKill = killIv;

  // ───────── 스킬 사용 ─────────
  function castSkill(sk) {
    sk.casts++;
    let dur = 0;
    const uses = [{ ratio: 1, final: 1, delay: 0 }];
    for (const e of sk.m.extras)
      uses.push({ ratio: e.ratio != null ? e.ratio : G.extraRatio, final: e.final || 1, delay: e.delay != null ? e.delay : 0.12 * uses.length, extra: true, spread: !!e.spread });
    // 분산형 추가 사용 (난사 · 양방향 · 연속 공격 · 삼연발 · 산탄 …): 기본 사용 + 이런 추가 사용이 범위 안 적을 나눠 맞힘 (중복 적중 없음).
    //   적 N 마리를 사용 U 개에 나눠 앞 사용부터 채움: 적 1 · 사용 5 → 1회만 적중 / 적 5 · 사용 5 → 5회 / 적 7 · 사용 5 → 2 · 2 · 1 · 1 · 1 (합 7)
    const group = uses.filter((u, i) => i === 0 || u.spread);
    if (group.length > 1) {
      const U = group.length, N = Math.max(0, Math.round(G.N)), cap = G.useCap > 0 ? G.useCap : Infinity;
      group.forEach((u, i) => { u.alloc = Math.min(cap, Math.floor(N / U) + (i < N % U ? 1 : 0)); });
    }
    for (const u of uses) {
      const c = makeCtx(sk, u);
      const d = sk.def.cast(c, sk.p, sk.m, u) || 0;
      dur = Math.max(dur, d + u.delay);
    }
    return dur;
  }

  function addPassiveBurn(dmg) {
    R.passive.total += dmg * G.N; R.passive.main += dmg;
    R.total += dmg * G.N; R.main += dmg;
    markMain();
    addBucket('passive', dmg * G.N);
    const e = R.passive.extra['화상 틱'] || (R.passive.extra['화상 틱'] = { total: 0, hits: 0 });
    e.total += dmg * G.N; e.hits++;
  }

  // ───────── 메인 루프 ─────────
  const steps = Math.round(T / DT);
  for (let step = 0; step < steps; step++) {
    now = step * DT;
    while (queue.length && queue[0].t <= now + 1e-9) queue.shift().fn();

    if (slaughter.n > 0) { slaughter.left -= DT; if (slaughter.left <= 0) slaughter.n = 0; }   // 살육의 열기 지속
    if (abHas('H15C') && abMax > 0 && abStacks() >= abMax) { abHold += DT; if (abHold >= pn('H15C', 'hold')) abCritical = true; }   // 임계 과열
    else { abHold = 0; abCritical = false; }

    if (now + 1e-9 >= nextKill) { onKill(); nextKill += killIv; }

    // 화상 틱
    if (burn.stacks > 0) {
      if (now > burn.expire + 1e-9) clearBurn();
      else {
        while (burn.stacks > 0 && now + 1e-9 >= burn.next) {
          const raw = burnTickRaw(burn.stacks, burn.brand);
          const dmg = fl(raw * received());
          R.passive.ticks++;
          addPassiveBurn(dmg);
          incinerate(G.N);
          burn.next += burnInterval();
        }
      }
    }
    if (burn.stacks > 0) R.burnTime += DT;
    if (isStunned()) R.stunTime += DT;

    for (const sk of runs) {
      if (sk.stackMode) {
        if (sk.stacks < sk.maxStacks) {   // 스택마다 따로 충전
          if (sk.rc <= 0) sk.rc = cooldownOf(sk);
          sk.rc -= DT;
          if (sk.rc <= 0) { sk.stacks++; sk.rc = sk.stacks < sk.maxStacks ? cooldownOf(sk) : 0; }
        }
        if (sk.stacks > 0 && now + 1e-9 >= sk.lock) {
          if (sk.stacks === sk.maxStacks) sk.rc = cooldownOf(sk);
          sk.hitFactor = 1;
          sk.stacks--;
          const dur = castSkill(sk);
          sk.lock = now + dur + G.reuseDelay;
        }
      } else {
        if (now + 1e-9 < sk.nextReady) continue;
        const dur = castSkill(sk);
        const cd = cooldownOf(sk);
        const reuse = sk.def.kind === 'auto' ? 0 : G.reuseDelay;
        sk.nextReady = (sk.def.cdAfterCast ? now + dur : now) + cd + reuse;
        sk.hitFactor = 1;
      }
    }
  }
  for (const sk of runs) {
    sk.pctAvg = sk.hitN ? sk.pctSum / sk.hitN : sk.m.dmgPct + sk.ups.dmg * bvs(sk.id, 'dmg');
    sk.finAvg = sk.hitN ? sk.finSum / sk.hitN : sk.m.finalMul;
    sk.cdEff = cooldownOf(sk);
  }
  R.dps = R.total / T;
  R.burnUptime = R.burnTime / T;
  R.stunUptime = R.stunTime / T;
  R.burn = { dps: burnDps, dur: burnDur, max: burnMax, pct: burnPctSelf };
  return R;
}

if (typeof module !== 'undefined') module.exports = { runSim, mergeMods, paramValues, comboAvailable };
