// 밸런스 시뮬레이터 데이터 v2 — 게임 코드 (Assets/Scripts/Skills/ErnStray/*.cs) + 노션 스킬 스펙 (2026-11-23) 의 수치를 옮긴 것.
// 값을 바꾸고 싶으면 이 파일의 params 만 고치면 되고, 화면에서도 바로 바꿔 볼 수 있다.
// 게임과 다르게 단순화한 곳은 note 에 적음. 이동 · 위치 · 연출만 있는 효과는 "시뮬레이터 영향 없음" 으로 표시.
//
// 스킬 정의
//   id, name, kind(auto|manual|move), cd(기본 쿨타임), cdAfterCast(true = 시전이 끝난 뒤부터 쿨타임), stacks(기본 스택 수, 있으면 스택형)
//   params: { 키: {l: 이름, v: 기본값, u: 단위} }   ← 타이밍 · 피해 · 기절 시간 등 전부 화면에서 수정 가능
//   cast(c, p, m, u): 시전 1회의 사건. c.at(시간, 함수) 로 예약, c.hit(초기 피해량, 옵션) = 일반 피해, c.fixed(양, 옵션) = 고정 피해.
//                     반환값 = 시전이 끝나는 시간(초). 추가 사용 (m.extras) 은 엔진이 cast 를 다시 불러 줌 (u.final · u.ratio)
//   onKill(c, p, m): 적을 처치할 때마다 (죽음의 메아리 같은 처치 효과)
//   specials: [{id, lv(5|10|15), name, desc, mods, note}]    combos: [{id, name, requires | requiresAny, desc, mods}]
//
// c.hit 옵션: label, single(주 대상만), hits(맞는 횟수 직접 지정), stun(초), burn:false(화상 부여 안 함), burnAdd, dot(지속 피해), extraPct, extraFinal, vuln:{per,max,key}
// mods 키: dmgPct(합) finalMul(곱) cdMul(곱) vsBurn(곱) vsStun(곱) stunAdd(합) countAdd(합) intervalMul(곱) delayMul(곱) durAdd(합) durPct(합)
//          hitCdRed(합, 적중 시 남은 쿨타임 비율 감소) hitCdSec(합, 적중 시 초 감소) burnAdd(합) stacksAdd(합) extras:[{final,ratio,delay}] extrasSet(통째로 교체)
//          nullCd:{base,perPct,perStack} (쿨타임 감소 무효화: 최종 ×(1+base+perPct×무효화한 감소% + perStack×특수 강화로 늘린 스택)) vuln vulnBurning flags:{…}

const BASE_GLOBAL = {
  T:           { l: '시뮬레이션 시간', v: 60, u: '초' },
  N:           { l: '범위 안 적 수 (광역 피해 배수)', v: 10, u: '마리' },
  enemyHp:     { l: '주 대상 체력', v: 5000, u: '' },
  boss:        { l: '주 대상이 보스 (소각 0.4%)', v: 0, u: '0 = 일반 · 1 = 보스' },
  dmgMul:      { l: '전역 최종 피해량 (기타)', v: 1, u: '배' },
  dmgPct:      { l: '전역 피해량 % 합 (기타)', v: 0, u: '(0.1 = 10%)' },
  cdMul:       { l: '전역 쿨타임 배율 (기타)', v: 1, u: '배 (0.9 = 10% 단축)' },
  burnPct:     { l: '화상 전역 피해량 % 합 (기타)', v: 0, u: '(0.1 = 10%)' },
  reuseDelay:  { l: '수동 스킬 쿨타임 뒤 사용 지연', v: 0.3, u: '초' },
  killRate:    { l: '초당 처치 수 (처치 효과용: 죽음의 메아리 · 살육의 열기 · 불길의 순환 · 확산되는 불길)', v: 0, u: '마리/초 · 0 = 처치 효과 미적용 (기본)' },
  blastTargets:{ l: '폭발 하나가 맞히는 적 수 (화상 폭발 · 죽음의 메아리 · 소형 폭발, 범위 안 적 수를 넘지 않음)', v: 3, u: '마리' },
  extraRatio:  { l: '추가 사용 · 추가 소용돌이가 맞히는 적 비율', v: 1, u: '0~1 (1 = 모두)' },
  burnShare:   { l: '범위 안 적 중 화상 상태 비율 (흡수 가열 스택)', v: 1, u: '0~1' },
  projHit:     { l: '유도 없는 검기 · 투사체 명중률 (유도 검기는 100%)', v: 0.5, u: '0~1 [임시]' },
  minDmg:      { l: '피해 1회 최소 피해 (게임 규칙: 0 보다 큰 피해는 최소 1)', v: 1, u: '1 = 현재 게임 규칙 · 0 = 내림으로 0 허용' },
  useCap:      { l: '분산형 추가 사용 · 사용 1회가 맞힐 수 있는 적 수 상한', v: 0, u: '0 = 제한 없음 (적이 많으면 한 사용이 여럿을 맞힘)' },
  melt:        { l: '받는 최종 피해량: 화상 스택당 (융해 없이 직접)', v: 0, u: '(0.02 = 2%)' },
};

// 아이템 · 세트 효과에 넣을 수 있는 항목 (나중에 아이템이 들어오면 여기서 고르기만 하면 됨 — 새 항목이 필요하면 sim.js 의 ITEM_SUM / ITEM_PRODUCT 에 추가)
const ITEM_FIELDS = [
  { k: 'dmgPct',       l: '피해량 %',            hint: '0.1 = +10% (합연산)' },
  { k: 'finalMul',     l: '최종 피해량 배율',     hint: '1.1 = ×1.1 (곱연산)' },
  { k: 'cdMul',        l: '쿨타임 배율',          hint: '0.9 = 10% 단축 (곱연산)' },
  { k: 'initialAdd',   l: '초기 피해량 +',        hint: '모든 피해의 초기 피해량에 더함' },
  { k: 'flatAdd',      l: '추가 피해량 +',        hint: '피해량 % 적용 뒤 더하는 고정 수치' },
  { k: 'burnPct',      l: '화상 피해량 %',        hint: '0.2 = +20%' },
  { k: 'dotPct',       l: '지속 피해 피해량 %',   hint: '0.2 = +20%' },
  { k: 'stunPct',      l: '기절 지속 시간 %',     hint: '0.2 = +20%' },
  { k: 'burnDps',      l: '화상 초당 초기 피해량 +', hint: '' },
  { k: 'burnStacks',   l: '화상 최대 스택 +',     hint: '' },
  { k: 'burnDuration', l: '화상 지속시간 +(초)',  hint: '' },
];

// 보스 체력 프리셋 (사용자 2026-11-23). 체력은 페이즈마다 따로 (1페이즈를 깎으면 2페이즈가 시작되는 구조)
const BOSS_PRESETS = [
  { id: 'gan_easy1', name: '가니악 벤데스트라 · 이지 1페이즈', hp: 1000000, group: '가니악 벤데스트라', phase: 1, diff: '이지' },
  { id: 'gan_norm1', name: '가니악 벤데스트라 · 노말 1페이즈', hp: 1500000, group: '가니악 벤데스트라', phase: 1, diff: '노말' },
  { id: 'gan_norm2', name: '가니악 벤데스트라 · 노말 2페이즈', hp: 2000000, group: '가니악 벤데스트라', phase: 2, diff: '노말' },
  { id: 'kag_easy1', name: '카구츠치 · 이지 1페이즈', hp: 2500000, group: '카구츠치', phase: 1, diff: '이지' },
  { id: 'kag_norm1', name: '카구츠치 · 노말 1페이즈', hp: 4000000, group: '카구츠치', phase: 1, diff: '노말' },
  { id: 'kag_norm2', name: '카구츠치 · 노말 2페이즈', hp: 5500000, group: '카구츠치', phase: 2, diff: '노말' },
];

// 기본 강화 1회당 증가량 (스킬마다 따로 조정 가능: S.bv['s:스킬id'] · 패시브는 S.bv['p:fs|hf|ab'])
const BASIC_DEF = {
  skill: { dmg: 0.1, cd: 0.025 },                       // 피해량 강화 +10% · 쿨타임 강화 -2.5% (범위 강화 +10% 는 시뮬레이터에 영향 없음)
  fs: { dps: 2, stk: 1, dur: 1 },                       // 화염의 검: 화상 초기 피해량 +2 · 최대 스택 +1 · 지속시간 +1초
  hf: { fin: 0.02, mob: 0.02, dot: 0.1 },               // 옥염: 최종 피해량 +2%p · 쿨타임 감소 +2% · 지속 피해 피해량 +10%
  ab: { fin: 0.01, cd: 0.002, stk: 1 },                 // 흡수 가열: 스택당 최종 +1%p · 스택당 쿨감 +0.2%p · 최대 스택 +1
};
const BASIC_LBL = {
  skill: { dmg: ['피해량 강화 1회당 증가', '0.1 = +10%'], cd: ['쿨타임 강화 1회당 감소', '0.025 = 2.5%'] },
  fs: { dps: ['화상 피해 강화 1회당 초기 피해량', ''], stk: ['최대 스택 강화 1회당 스택', ''], dur: ['지속시간 강화 1회당', '초'] },
  hf: { fin: ['최종 피해 강화 1회당 최종 피해량', '0.02 = +2%p'], mob: ['기동력 강화 1회당 쿨타임 감소', '0.02 = 2%'], dot: ['지속 피해 강화 1회당 지속 피해 피해량', '0.1 = +10%'] },
  ab: { fin: ['최종 피해 강화 1회당 스택당 최종 피해량', '0.01 = +1%p'], cd: ['쿨타임 강화 1회당 스택당 쿨타임 감소', '0.002 = 0.2%p'], stk: ['최대 스택 강화 1회당 스택', ''] },
};

const sp = (id, lv, name, desc, mods, note, nums) => ({ id, lv, name, desc, mods: mods || {}, note, nums: nums || {} });
/** 조정 가능한 수치 한 칸 (화면에서 수정 · 시뮬레이터가 cast 안에서 NV 로 읽음) */
const n_ = (l, v, u) => ({ l, v, u });
/** 특수 강화 · 조합의 조정 수치 읽기 (엔진이 기본값 + 화면에서 바꾼 값을 합쳐 m.nums 에 넣어 줌) */
const NV = (m, id, k) => m.nums[id + '.' + k];
const NONE = '시뮬레이터 영향 없음 (범위 · 이동 · 연출)';
const fire2 = (c, bonus) => (c.burning() ? 1 + (bonus == null ? 1 : bonus) : 1);   // 화상 상태 적에게 최종 피해량 +bonus (기본 +100%, 고정 피해에 직접 곱함)

// ───────────────── 패시브 ─────────────────
const PASSIVES = {
  fs: {
    id: 'fs', name: '화염의 검 (화상)',
    params: {
      dps:       { l: '화상 초당 초기 피해량 (스택당)', v: 2, u: '' },
      duration:  { l: '화상 지속시간', v: 3, u: '초' },
      interval:  { l: '화상 피해 간격', v: 1, u: '초' },
      maxStacks: { l: '화상 최대 스택', v: 5, u: '' },
    },
    basic: [['dps', '화상 초기 피해량 +2'], ['stk', '최대 스택 +1'], ['dur', '지속 시간 +1초']],
    specials: [
      sp('B5A', 5, '융해', '화상 스택당 받는 최종 피해량 +2%', null, null, { per: n_('스택당 받는 최종 피해량', 0.02, '0.02 = 2%') }),
      sp('B5B', 5, '기름붓기', '화상 피해량 -25%, 지속 시간 +100%', null, null, { dmg: n_('화상 피해량 %', -0.25, '-0.25 = -25%'), dur: n_('지속 시간 증가', 1, '1 = +100%') }),
      sp('B5C', 5, '확산되는 불길', '화상 상태 적 사망 시 화상 +1, 최대 스택 +10', null, '사망 시 확산은 "초당 처치 수" 로 모델링', { stacks: n_('최대 스택 +', 10), give: n_('사망 시 부여 스택', 1) }),
      sp('B10A', 10, '일촉즉발', '최대 스택 도달 시 화상 일괄 적용 · 스택 소모 (대상당 쿨타임 3초)', null, '대상당 쿨타임 안에는 최대 스택이어도 다시 터지지 않음', { icd: n_('대상당 쿨타임', 3, '초') }),
      sp('B10B', 10, '살아있는 폭탄', '화상 상태 적 직접 피해 적중 시 폭발 (화상 총 피해량 = 1틱 × 스택 의 100% 고정)', null, '폭발이 맞히는 적 = "폭발 하나가 맞히는 적 수"', { ratio: n_('화상 총 피해량 비율', 1) }),
      sp('B10C', 10, '끈적한 화염', '화상 상태 적 이동 속도 -50%', null, NONE),
      sp('B15A', 15, '영원의 불꽃', '화상 영구 지속, 지속시간 스탯 1초당 화상 피해량 +50%, 최대 스택 +10', null, null, { perSec: n_('지속시간 1초당 화상 피해량', 0.5, '0.5 = +50%'), stacks: n_('최대 스택 +', 10) }),
      sp('B15B', 15, '핵융합', '화상 지속 시간 3초 실적용, 줄어든 1초당 화상 피해량 +100%', null, null, { dur: n_('실제 지속 시간', 3, '초'), perSec: n_('줄어든 1초당 화상 피해량', 1, '1 = +100%') }),
      sp('B15C', 15, '화염 낙인', '화상 상태 적 직접 피해 적중 시 화상 피해량 +10% (최대 30스택)', null, null, { per: n_('낙인 1스택당 화상 피해량', 0.1, '0.1 = +10%'), max: n_('최대 스택', 30) }),
    ],
  },
  hf: {
    id: 'hf', name: '옥염 (최종 피해량)',
    params: {},
    basic: [['fin', '최종 피해량 +2%p'], ['mob', '이동속도 · 쿨타임 감소 +2%'], ['dot', '지속 피해 피해량 +10%']],
    specials: [
      sp('Y5A', 5, '겁화', '모든 화상 최종 피해량 +50%', null, null, { burn: n_('화상 최종 피해량', 0.5, '0.5 = +50%') }),
      sp('Y5B', 5, '화력 증폭', '최종 피해량 +30%', null, null, { final: n_('최종 피해량', 0.3, '0.3 = +30%') }),
      sp('Y5C', 5, '살육의 열기', '처치 시 최종 피해량 +5% (5초, 최대 10스택)', null, null, { per: n_('처치당 최종 피해량', 0.05, '0.05 = +5%'), max: n_('최대 스택', 10), time: n_('지속 시간', 5, '초') }),
      sp('Y10A', 10, '홍련지옥', '모든 화상 최종 피해량 +100%', null, null, { burn: n_('화상 최종 피해량', 1, '1 = +100%') }),
      sp('Y10B', 10, '극화', '최종 피해량 +50%', null, null, { final: n_('최종 피해량', 0.5, '0.5 = +50%') }),
      sp('Y10C', 10, '불길의 순환', '화상 상태 적 사망 시 모든 스킬 남은 쿨타임 2.5% 감소', null, null, { ratio: n_('남은 쿨타임 감소', 0.025, '0.025 = 2.5%') }),
      sp('Y15A', 15, '소각', '지속 피해가 적용될 때마다 대상 최대 체력 1% 고정 피해 (보스 0.4%)', null, null, { pct: n_('최대 체력 비율', 0.01, '0.01 = 1%'), boss: n_('보스 대상 비율', 0.004) }),
      sp('Y15B', 15, '극한 연소', '최종 피해량 +75%', null, null, { final: n_('최종 피해량', 0.75, '0.75 = +75%') }),
      sp('Y15C', 15, '화염 공명', '적중 시 궁극기 게이지 +1%', null, NONE + ' (궁극기 게이지는 시뮬레이터에 없음)'),
    ],
  },
  ab: {
    id: 'ab', name: '흡수 가열 (화상 적 수 스택)',
    params: {},
    basic: [['fin', '스택당 최종 피해량 +1%p'], ['cd', '스택당 쿨타임 감소 +0.2%p'], ['stk', '최대 스택 +1']],
    specials: [
      sp('H5A', 5, '불씨 점화', '스택당 모든 화상 피해량 +10%', null, null, { per: n_('스택당 화상 피해량', 0.1, '0.1 = +10%') }),
      sp('H5B', 5, '열기 추진', '스택당 이동속도 +5%', null, NONE),
      sp('H5C', 5, '압축 연료', '최대 스택 절반, 모든 최종 효과량 +100%', null, null, { halve: n_('최대 스택 배율', 0.5), bonus: n_('최종 효과량 증가', 1, '1 = +100%') }),
      sp('H10A', 10, '열폭주', '스택당 모든 지속 피해 피해량 +10%', null, null, { per: n_('스택당 지속 피해 피해량', 0.1, '0.1 = +10%') }),
      sp('H10B', 10, '에너지 흡수', '스택당 궁극기 게이지 획득량 +5%', null, NONE),
      sp('H10C', 10, '열기 응축', '감지 범위 -50%, 모든 최종 효과량 +100%', null, '감지 범위 감소는 "범위 안 적 중 화상 비율" 로 직접 조절', { bonus: n_('최종 효과량 증가', 1, '1 = +100%') }),
      sp('H15A', 15, '가속 연소', '스택당 모든 지속 피해 간격 -0.05초 (최소 0.1초)', null, null, { per: n_('스택당 간격 감소', 0.05, '초') }),
      sp('H15B', 15, '이열치열', '스택당 모든 피해 흡혈 +0.05%p', null, NONE),
      sp('H15C', 15, '임계 과열', '최대 스택 5초 유지 시 모든 최종 효과량 +100%', null, null, { hold: n_('최대 스택 유지 시간', 5, '초'), bonus: n_('최종 효과량 증가', 1, '1 = +100%') }),
    ],
  },
};

// ───────────────── 스킬 ─────────────────
const SKILLS = [
  // ═════════ 자동 ═════════
  { id: 'fireslash', name: '파이어 슬래시', kind: 'auto', cd: 2,
    params: { dmg: { l: '초기 피해량', v: 10 }, delay: { l: '적중까지', v: 0.1, u: '초' } },
    cast(c, p, m, u) {
      c.at(p.delay, () => {
        c.hit(p.dmg, { label: '베기' });
        // 검기 발사: 모든 대상 관통 → 이 사용이 맞히는 적 (분산형 추가 사용이면 그 몫) 전부에게, 유도가 없으니 명중률 적용
        if (m.flags.wave) c.fixed(c.A(p.dmg) * NV(m, 'FSL10A', 'ratio'), { label: '검기', hits: Math.max(1, Math.round((u.alloc != null ? u.alloc : c.G.N) * NV(m, 'FSL10A', 'pierce'))), rate: c.G.projHit });
        if (m.flags.quick) c.reduceCdRatio(NV(m, 'FSL10C', 'ratio'));
      });
      return p.delay;
    },
    onKill(c, p, m) { if (m.flags.echo) for (let i = 0; i < NV(m, 'FSL15C', 'count'); i++) c.fixed(c.A(p.dmg) * NV(m, 'FSL15C', 'ratio'), { label: '죽음의 메아리', hits: c.killBlast }); },
    specials: [
      sp('FSL5A', 5, '대상 추적', '가장 가까운 적을 추적, 피해량 +50%', { dmgPct: 0.5 }),
      sp('FSL5B', 5, '난사', '기존 공격을 난사 (추가 사용 +2) — 노션 키워드 "난사": 무작위 방향과 각도로 공격 분산, 쿨타임 감소 +10%', { extras: [{ ratio: 1, delay: 0.1, spread: 1 }, { ratio: 1, delay: 0.2, spread: 1 }], cdMul: 0.9 }),
      sp('FSL5C', 5, '양방향 공격', '공격 반대 방향으로 추가 사용, 피해량 +25%', { extras: [{ spread: 1 }], dmgPct: 0.25 }),
      sp('FSL10A', 10, '검기 발사', '공격 방향으로 검기 (모든 대상 관통, 총 피해량 100% 고정)', { flags: { wave: 1 } }, null, { ratio: n_('총 피해량 비율', 1), pierce: n_('관통하는 적 비율', 1, '1 = 범위 안 모든 적을 관통 · 0.5 = 절반') }),
      sp('FSL10B', 10, '연속 공격', '120도 간격으로 추가 사용 +2, 쿨타임 감소 +10%', { extras: [{ spread: 1 }, { spread: 1 }], cdMul: 0.9 }),
      sp('FSL10C', 10, '빠른 공격', '종료 시 남은 쿨타임 50% 감소', { flags: { quick: 1 } }, null, { ratio: n_('남은 쿨타임 감소', 0.5, '0.5 = 50%') }),
      sp('FSL15A', 15, '삼연발', '양쪽 60도로 1회씩 추가 사용, 최종 피해량 +25%', { extras: [{ spread: 1 }, { spread: 1 }], finalMul: 1.25 }),
      sp('FSL15B', 15, '묵직한 일격', '최종 피해량 +100%, 쿨타임 감소 무효화 (무효화 1%당 최종 +2%), 범위 +50%', { nullCd: { base: 1, perPct: 0.02 } }),
      sp('FSL15C', 15, '죽음의 메아리', '처치 시 120도 간격 참격 3개 (총 피해량 100% 고정)', { flags: { echo: 1 } }, '처치 빈도 = "초당 처치 수"', { ratio: n_('참격당 총 피해량 비율', 1), count: n_('참격 수', 3) }),
    ] },

  { id: 'solarslash', name: '솔라 슬래시', kind: 'auto', cd: 5,
    params: { slash: { l: '베기 초기 피해량', v: 5 }, expl: { l: '폭발 초기 피해량', v: 12 }, explDelay: { l: '폭발까지', v: 0.45, u: '초' }, burnTime: { l: '타오름 지속 시간', v: 5, u: '초' } },
    cast(c, p, m) {
      const tot = c.A(p.slash) + c.A(p.expl);
      c.at(0, () => c.hit(p.slash, { label: '베기' }));
      if (m.flags.burnField) {   // 타오름: 폭발 제거, 총 피해량 100% 지속 고정 피해
        const iv = c.dotIv(1), n = Math.max(1, Math.floor(p.burnTime / iv + 1e-4));
        for (let i = 0; i < n; i++) c.at(i * iv, () => c.fixed(tot * NV(m, 'SO10B', 'ratio') / n, { label: '타오름', dot: true }));
        return p.burnTime;
      }
      const d = m.flags.sunspot ? 0 : p.explDelay;
      c.at(d, () => c.hit(p.expl, { label: '폭발', stun: m.flags.concuss ? NV(m, 'SO5B', 'stun') : 0 }));
      let end = d;
      if (m.flags.chainBlast) for (let k = 1; k <= NV(m, 'SO15A', 'count'); k++) { const gap = NV(m, 'SO15A', 'gap'); c.at(d + gap * k, () => c.hit(p.expl, { label: '연속 폭발' })); end = d + gap * k; }
      return end;
    },
    specials: [
      sp('SO5A', 5, '충격 발산', '넉백 범위 +100%, 피해량 +50%', { dmgPct: 0.5 }),
      sp('SO5B', 5, '뇌진탕', '넉백된 적이 다른 적과 충돌하면 기절 1초, 최종 피해량 +25%', { flags: { concuss: 1 }, finalMul: 1.25 }, '충돌은 폭발에 맞은 모든 적이 한다고 가정', { stun: n_('기절', 1, '초') }),
      sp('SO5C', 5, '느린 회복', '적중 시 이동 속도 -50%, 2초', null, NONE),
      sp('SO10A', 10, '메아리', '발동 횟수 +1, 쿨타임 감소 -25%', { extras: [{ ratio: 1, delay: 0.6 }], cdMul: 1.25 }, '쿨타임 감소 -25% = 쿨타임 ×1.25 로 해석'),
      sp('SO10B', 10, '타오름', '폭발 제거, 지속 피해로 전환 (총 피해량 100% 고정), 범위 +50%, 최종 피해량 +25%', { flags: { burnField: 1 }, finalMul: 1.25 }, null, { ratio: n_('총 피해량 비율', 1) }),
      sp('SO10C', 10, '흑점 폭발', '폭발 즉시 발생, 넉백 범위 +50%, 최종 피해량 +100%', { flags: { sunspot: 1 }, finalMul: 2 }),
      sp('SO15A', 15, '연속 폭발', '종료 시 0.5초 간격으로 2회 폭발, 쿨타임 감소 +25%', { flags: { chainBlast: 1 }, cdMul: 0.75 }, null, { count: n_('추가 폭발 횟수', 2), gap: n_('폭발 간격', 0.5, '초') }),
      sp('SO15B', 15, '초신성', '넉백 범위 +50% → 피해량 +50%, 쿨타임 감소 -50%', { dmgPct: 0.5, cdMul: 1.5 }, '쿨타임 감소 -50% = 쿨타임 ×1.5 로 해석'),
      sp('SO15C', 15, '플레어', '최종 피해량 +100%, 쿨타임 감소 +10%', { finalMul: 2, cdMul: 0.9 }),
    ] },

  { id: 'fireball', name: '파이어 볼', kind: 'auto', cd: 3,
    params: { direct: { l: '직격 초기 피해량', v: 3 }, expl: { l: '폭발 초기 피해량', v: 4 }, travel: { l: '날아가는 시간', v: 0.3, u: '초' }, hitGap: { l: '관통 · 도탄 적중 사이', v: 0.1, u: '초' } },
    cast(c, p, m) {
      const fw = !!m.flags.fireworks;
      const hits = 1 + (m.flags.pierce && !fw ? 2 : 0) + (m.flags.ricochet && !fw ? 2 : 0);
      const tot = c.A(p.direct) + c.A(p.expl);
      const nullCount = (m.flags.spread ? 1 : 0) + (m.flags.pierce ? 1 : 0) + (m.flags.ricochet ? 1 : 0);
      const xf = fw ? 1 + NV(m, 'FB15B', 'perNull') * nullCount : 1;   // 폭죽놀이: 무효화 1당 최종 +50%
      let end = p.travel;
      for (let h = 0; h < hits; h++) {
        const t = p.travel + p.hitGap * h;
        end = t;
        c.at(t, () => {
          const wasBurn = c.burning();
          c.hit(p.direct, { label: '직격', single: true, extraFinal: xf, stun: m.flags.strike && wasBurn ? NV(m, 'FB10C', 'stun') : 0 });
          c.hit(p.expl, { label: '폭발', extraFinal: xf });
          if (m.flags.cluster) for (let k = 0; k < NV(m, 'FB10A', 'count'); k++) c.fixed(tot * NV(m, 'FB10A', 'ratio'), { label: '집속탄', single: true });
          if (m.flags.crimson) for (let k = 0; k < NV(m, 'FB15A', 'count'); k++) c.fixed(tot * NV(m, 'FB15A', 'ratio'), { label: '홍염', hits: Math.min(c.G.N, 3) });
        });
      }
      if (m.flags.chainExpl) { const g = NV(m, 'FB10B', 'gap'); [[g, NV(m, 'FB10B', 'r1')], [2 * g, NV(m, 'FB10B', 'r2')]].forEach(([dt, r]) => { c.at(end + dt, () => c.fixed(tot * r, { label: '연속 폭발' })); }); end += 2 * g; }
      if (fw) c.at(end + 0.1, () => { for (let k = 0; k < 2 * NV(m, 'FB15B', 'count'); k++) c.fixed(tot * NV(m, 'FB15B', 'ratio'), { label: '폭죽놀이', single: true }); });   // 화염구 12개 × (적중 + 종료 시 추가 발동)
      return end;
    },
    specials: [
      sp('FB5A', 5, '산탄', '양쪽 각 30도 방향으로 1회씩 추가 사용', { extras: [{ spread: 1 }, { spread: 1 }], flags: { spread: 1 } }),
      sp('FB5B', 5, '관통', '관통 +2 (직격이 3명까지 · 적중마다 폭발)', { flags: { pierce: 1 } }),
      sp('FB5C', 5, '도탄', '도탄 +2', { flags: { ricochet: 1 } }),
      sp('FB10A', 10, '집속탄', '적중 시 화염구 3개 (총 피해량 60% 고정)', { flags: { cluster: 1 } }, null, { count: n_('화염구 수', 3), ratio: n_('화염구당 총 피해량 비율', 0.6) }),
      sp('FB10B', 10, '연속 폭발', '종료 시 폭발 2회 추가 (총 피해량 60% · 90% 고정)', { flags: { chainExpl: 1 } }, null, { r1: n_('1회 폭발 총 피해량 비율', 0.6), r2: n_('2회 폭발 총 피해량 비율', 0.9), gap: n_('폭발 간격', 0.5, '초') }),
      sp('FB10C', 10, '화염강타', '화상 상태 적 적중 시 기절 +1초, 기절 상태 적 최종 피해량 +25%', { flags: { strike: 1 }, vsStun: 1.25 }, null, { stun: n_('화상 상태 적 적중 시 기절', 1, '초') }),
      sp('FB15A', 15, '홍염', '적중 시 화염구 3개 (총 피해량 60% 고정, 관통 후 복귀)', { flags: { crimson: 1 } }, null, { count: n_('화염구 수', 3), ratio: n_('화염구당 총 피해량 비율', 0.6) }),
      sp('FB15B', 15, '폭죽놀이', '도탄 · 관통 · 공격 횟수 무효화, 종료 시 화염구 12개 (총 피해량 20% 고정, 종료 시 추가 발동), 무효화 1당 최종 +50%', { flags: { fireworks: 1, noExtras: 1 } }, '무효화 수 = 5레벨에서 고른 산탄 · 관통 · 도탄 (0~1)', { count: n_('화염구 수', 12), ratio: n_('화염구당 총 피해량 비율', 0.2), perNull: n_('무효화 1당 최종 피해량', 0.5, '0.5 = +50%') }),
      sp('FB15C', 15, '초거대', '최종 피해량 +50%, 범위 +50%', { finalMul: 1.5 }),
    ] },

  { id: 'firebuster', name: '파이어 버스터', kind: 'auto', cd: 10,
    params: { dmg: { l: '초기 피해량', v: 20 }, delay: { l: '폭발까지', v: 0.2, u: '초' } },
    cast(c, p, m) {
      const tot = c.A(p.dmg);
      c.at(p.delay, () => {
        const wasBurn = c.burning(), bt = c.burnTick();   // 연쇄 폭발: 화상 총 피해량 = 1틱 × 스택
        c.hit(p.dmg, { label: '폭발', stun: m.flags.concuss ? NV(m, 'FU10B', 'stun') : 0 });
        if (m.flags.overheat && wasBurn) c.reduceCdRatio(NV(m, 'FU10C', 'cdRed'), Math.max(1, Math.round(c.G.N * c.G.burnShare)));   // 과열 반응: 화상 상태 적 적중마다 남은 쿨타임 감소
        if (m.flags.chain && wasBurn)   // 연쇄 폭발: 화상 상태 적마다 폭발 (대상의 화상 총 피해량 100% 고정)
          c.fixed(bt * NV(m, 'FU15B', 'ratio'), { label: '연쇄 폭발', dot: true, hits: Math.max(1, Math.round(c.G.N * c.G.burnShare)) * c.blast });
      });
      let end = p.delay;
      if (m.flags.double) { const g = NV(m, 'FU10A', 'delay'); c.at(p.delay + g, () => c.fixed(tot * NV(m, 'FU10A', 'ratio'), { label: '이중 폭발' })); end += g; }
      if (m.flags.series) { const cn = NV(m, 'FU15C', 'count'), g = NV(m, 'FU15C', 'gap'); for (let k = 1; k <= cn; k++) c.at(p.delay + g * k, () => c.fixed(tot * NV(m, 'FU15C', 'ratio'), { label: '연속 폭발' })); end += g * cn; }
      return end;
    },
    specials: [
      sp('FU5A', 5, '열파', '범위 +30%, 넉백 +50%, 피해량 +25%', { dmgPct: 0.25 }),
      sp('FU5B', 5, '점화', '적중 시 적용된 모든 화상 +2', { burnAdd: 2 }),
      sp('FU5C', 5, '고농축', '범위 -25%, 최종 피해량 +75%', { finalMul: 1.75 }),
      sp('FU10A', 10, '이중 폭발', '종료 시 0.5초 뒤 폭발 1회 (총 피해량 100% 고정)', { flags: { double: 1 } }, null, { delay: n_('추가 폭발까지', 0.5, '초'), ratio: n_('총 피해량 비율', 1) }),
      sp('FU10B', 10, '뇌진탕', '넉백된 적이 다른 적에게 충돌하면 기절 1초, 최종 피해량 +25%', { flags: { concuss: 1 }, finalMul: 1.25 }, '충돌은 폭발에 맞은 모든 적이 한다고 가정', { stun: n_('기절', 1, '초') }),
      sp('FU10C', 10, '과열 반응', '화상 상태 적 적중 시 최종 피해량 +50%, 남은 쿨타임 2.5% 감소', { vsBurn: 1.5, flags: { overheat: 1 } }, '쿨타임 감소는 화상 상태 적 적중마다 적용 [해석]', { cdRed: n_('화상 상태 적 적중 시 남은 쿨타임 감소', 0.025, '0.025 = 2.5%') }),
      sp('FU15A', 15, '초신성', '범위 +100%, 최종 피해량 +150%, 쿨타임 +100%', { finalMul: 2.5, cdMul: 2 }),
      sp('FU15B', 15, '연쇄 폭발', '화상 상태 적 적중 시 폭발 (대상의 화상 총 피해량 = 1틱 × 스택 의 100% 고정)', { flags: { chain: 1 } }, '폭발 하나가 맞히는 적 수 = 전역 설정', { ratio: n_('화상 총 피해량 비율', 1) }),
      sp('FU15C', 15, '연속 폭발', '종료 시 0.25초 간격 폭발 3회 (총 피해량 40% 고정)', { flags: { series: 1 } }, null, { count: n_('폭발 횟수', 3), gap: n_('폭발 간격', 0.25, '초'), ratio: n_('폭발당 총 피해량 비율', 0.4) }),
    ] },

  { id: 'fireblade', name: '파이어 블레이드', kind: 'auto', cd: 15, cdAfterCast: true,
    params: {
      first: { l: '첫 베기 초기 피해량', v: 5 }, firstDelay: { l: '첫 베기까지', v: 0.24, u: '초' },
      chain: { l: '연속 베기 초기 피해량', v: 3 }, count: { l: '연속 베기 횟수', v: 5 }, interval: { l: '연속 베기 간격', v: 0.12, u: '초' },
      expl: { l: '폭발 초기 피해량', v: 20 }, explDelay: { l: '마지막 베기 뒤 폭발까지', v: 0.2, u: '초' },
    },
    cast(c, p, m) {
      const n = p.count + m.countAdd;
      c.at(p.firstDelay, () => c.hit(p.first, { label: '첫 베기' }));
      let t = p.firstDelay + 0.05;
      if (!m.flags.noChain) for (let i = 0; i < n; i++) { c.at(t, () => c.hit(p.chain, { label: '연속 베기', vuln: m.vuln })); t += p.interval; }
      const xf = m.flags.focus ? 1 + NV(m, 'FBL10A', 'per') * n : 1;   // 참격 집중: 삭제한 연속 베기 1회당 폭발 +75%
      const te = t + p.explDelay;
      c.at(te, () => c.hit(p.expl, { label: '폭발', extraFinal: xf }));
      let end = te;
      if (m.flags.chainBlast) { const g = NV(m, 'FBL15B', 'delay'); c.at(te + g, () => c.hit(p.expl, { label: '연쇄 폭발' })); end = te + g; }
      if (m.flags.spread) {   // 확산: 폭발에 맞은 적마다 연속 베기 1회 + 폭발 (피해량은 원본 고정)
        const k = Math.min(c.G.N, NV(m, 'FBL5C', 'max'));
        c.at(te + 0.3, () => {
          c.fixed(c.A(p.chain), { label: '확산 연속 베기', hits: k * n });
          c.fixed(c.A(p.expl), { label: '확산 폭발', hits: k * c.blast });
        });
        end = Math.max(end, te + 0.3);
      }
      return end;
    },
    specials: [
      sp('FBL5A', 5, '난무', '연속 참격 공격 횟수 +5', { countAdd: 5 }),
      sp('FBL5B', 5, '회전 베기', '첫 베기를 회전 베기로, 범위 +50%, 최종 피해량 +50%', { finalMul: 1.5 }),
      sp('FBL5C', 5, '확산', '종료 시 폭발에 맞은 적마다 연속 베기와 폭발 (원본 피해량 고정, 최대 12명), 피해량 +25%', { flags: { spread: 1 }, dmgPct: 0.25 }, null, { max: n_('최대 적 수', 12) }),
      sp('FBL10A', 10, '참격 집중', '연속 베기 삭제, 삭제한 횟수 1당 폭발 +75%', { flags: { noChain: 1, focus: 1 } }, null, { per: n_('횟수 1당 폭발 피해량', 0.75, '0.75 = +75%') }),
      sp('FBL10B', 10, '흉터 새기기', '연속 베기 적중 시 받는 최종 피해량 +5% (최대 10스택)', { vuln: { per: 0.05, max: 10, key: 'scar' } }),
      sp('FBL10C', 10, '절삭 가속', '적중 시 남은 쿨타임 2.5% 감소', { hitCdRed: 0.025 }),
      sp('FBL15A', 15, '삼연발', '120도 각도로 2회 추가 사용, 피해량 +50%', { extras: [{ spread: 1 }, { spread: 1 }], dmgPct: 0.5 }),
      sp('FBL15B', 15, '연쇄 폭발', '폭발 종료 시 폭발 추가 발동', { flags: { chainBlast: 1 } }, null, { delay: n_('추가 폭발까지', 0.4, '초') }),
      sp('FBL15C', 15, '타오르는 참격', '화상 상태 적 적중 시 최종 피해량 +100%', { vsBurn: 2 }),
    ] },

  { id: 'firestorm', name: '파이어 스톰', kind: 'auto', cd: 14,
    params: { dot: { l: '소용돌이 초기 피해량 (틱당)', v: 2 }, dur: { l: '소용돌이 지속시간', v: 3, u: '초' }, interval: { l: '피해 간격', v: 1, u: '초' }, expl: { l: '폭발 초기 피해량', v: 15 } },
    cast(c, p, m) {
      const dur = Math.max(0.3, (p.dur + m.durAdd) * (1 + m.durPct));
      const iv = Math.max(0.1, c.dotIv(p.interval * m.intervalMul));
      const vortex = m.flags.eve ? NV(m, 'FS5C', 'vortex') : 1, blast = m.flags.eve ? NV(m, 'FS5C', 'blast') : 1;
      for (let t = 0; t < dur - 1e-6; t += iv) c.at(t, () => c.hit(p.dot, { label: '소용돌이', extraFinal: vortex, burn: false, dot: true }));
      if (!m.flags.noBlast) c.at(dur, () => c.hit(p.expl, { label: '폭발', extraFinal: blast }));
      return dur;
    },
    combos: [
      { id: 'FSC', name: '천염폭풍', requiresAny: [['FS10A', 'FS15B'], ['FS10B', 'FS15B']],
        desc: '폭발 삭제, 추가 소용돌이 10개 (쌍둥이 태풍 1개를 대체), 범위 -50%, 최종 피해량 +50%, 쿨타임 +25%',
        mods: { flags: { noBlast: 1 }, extrasSet: Array.from({ length: 10 }, () => ({})), finalMul: 1.5, cdMul: 1.25 } },
    ],
    specials: [
      sp('FS5A', 5, '거대한 폭풍', '범위 +30%, 지속 시간 +1초', { durAdd: 1 }),
      sp('FS5B', 5, '가속 회전', '피해 간격 -50%', { intervalMul: 0.5 }),
      sp('FS5C', 5, '폭풍전야', '소용돌이 최종 -75%, 폭발 최종 +100%', { flags: { eve: 1 } }, null, { vortex: n_('소용돌이 최종 피해량 배율', 0.25, '0.25 = -75%'), blast: n_('폭발 최종 피해량 배율', 2, '2 = +100%') }),
      sp('FS10A', 10, '고정 폭풍', '소용돌이가 플레이어를 따라오지 않음, 범위 +50%, 지속 시간 +100%', { durPct: 1 }),
      sp('FS10B', 10, '추적 폭풍', '소용돌이가 적을 추적, 쿨타임 감소 +25%', { cdMul: 0.75 }),
      sp('FS10C', 10, '타오르는 불씨', '화상 상태 적 적중 시 받는 최종 피해량 +5% (최대 10스택)', { vulnBurning: { per: 0.05, max: 10, key: 'ember' } }),
      sp('FS15A', 15, '폭심 붕괴', '지속 시간 -75%, 최종 피해량 +200%', { durPct: -0.75, finalMul: 3 }),
      sp('FS15B', 15, '쌍둥이 태풍', '주위 무작위 장소에 추가 소용돌이', { extras: [{}] }),
      sp('FS15C', 15, '초가속', '공격 간격 -75%', { intervalMul: 0.25 }),
    ] },

  // ═════════ 수동 ═════════
  { id: 'tripleslash', name: '트리플 슬래시', kind: 'manual', cd: 8,
    params: { dmg: { l: '베기 1회 초기 피해량', v: 8 }, count: { l: '내부 공격 횟수', v: 3 }, interval: { l: '베기 간격', v: 0.05, u: '초' }, embersTime: { l: '잔화의 검흔 지속', v: 3, u: '초' } },
    cast(c, p, m) {
      let n = p.count * (m.flags.frenzy ? NV(m, 'T5A', 'mult') : 1) + m.countAdd;
      let extraPct = 0;
      if (m.flags.ilseom) { extraPct = NV(m, 'T10B', 'perCount') * n; n = 1; }   // 일섬: 내부 공격 횟수 무효화, 1회당 피해량 +125%
      const tot = c.A(p.dmg, { extraPct }) * n;
      for (let i = 0; i < n; i++) c.at(i * p.interval, () => c.hit(p.dmg, { label: '베기', extraPct }));
      let end = n * p.interval;
      if (m.flags.embers) {   // 잔화의 검흔: 참격 3개가 남아 총 피해량 40% 고정 지속 피해
        const ticks = Math.max(1, Math.floor(p.embersTime / c.dotIv(1) + 1e-4));
        for (let s = 0; s < NV(m, 'T15C', 'count'); s++) for (let i = 0; i < ticks; i++) c.at(end + i, () => c.fixed(tot * NV(m, 'T15C', 'ratio') / (NV(m, 'T15C', 'count') * ticks), { label: '잔화의 검흔', dot: true }));
        end += p.embersTime;
      }
      return end;
    },
    combos: [
      { id: 'TC1', name: '츠바메가에시', requires: ['T10B', 'T15A'],
        desc: '최종 피해량 +500%, 쿨타임 감소 무효화 (1%당 최종 +10%), 쿨타임 +200%',
        mods: { nullCd: { base: 5, perPct: 0.1 }, cdMul: 3 } },
    ],
    specials: [
      sp('T5A', 5, '난도질', '내부 공격 횟수 3배', { flags: { frenzy: 1 } }, null, { mult: n_('내부 공격 횟수 배수', 3) }),
      sp('T5B', 5, '난개', '120도 간격으로 추가 사용 +2', { extras: [{ spread: 1 }, { spread: 1 }] }),
      sp('T5C', 5, '빠른 준비', '적중 시 남은 쿨타임 2.5% 감소', { hitCdRed: 0.025 }),
      sp('T10A', 10, '과부화', '범위 +50%, 피해량 +100%, 쿨타임 +25%', { dmgPct: 1, cdMul: 1.25 }),
      sp('T10B', 10, '일섬', '내부 공격 횟수 무효화, 무효화 1당 피해량 +125%', { flags: { ilseom: 1 } }, null, { perCount: n_('무효화 1당 피해량', 1.25, '1.25 = +125%') }),
      sp('T10C', 10, '고열 절삭', '대상 화상 스택당 최종 피해량 +5% (상한 75%)', { flags: { searing: 1 } }, null, { per: n_('스택당 최종 피해량', 0.05, '0.05 = +5%'), cap: n_('효과 상한', 0.75, '0.75 = 75%') }),
      sp('T15A', 15, '삼연발', '발동 횟수 +2', { extras: [{}, {}] }),
      sp('T15B', 15, '강제 발화', '적중 시 부여된 모든 화상 일괄 적용', { flags: { forceBurn: 1 } }),
      sp('T15C', 15, '잔화의 검흔', '종료 시 지속 피해 참격 3개 (총 피해량 40% 고정, 3초)', { flags: { embers: 1 } }, null, { ratio: n_('총 피해량 비율', 0.4), count: n_('참격 수', 3) }),
    ] },

  { id: 'firecharge', name: '파이어 차지', kind: 'move', cd: 10, stacks: 1,
    params: { dmg: { l: '초기 피해량', v: 4 }, stun: { l: '기본 기절', v: 0, u: '초' }, dash: { l: '돌진 시간', v: 0.25, u: '초' }, fieldTime: { l: '장판 · 오오라 지속', v: 3, u: '초' } },
    cast(c, p, m) {
      const tot = c.A(p.dmg);
      c.at(p.dash * 0.5, () => c.hit(p.dmg, { label: '돌진', stun: p.stun + m.stunAdd }));
      const iv = 0.5, n = Math.max(1, Math.round(p.fieldTime / iv));
      if (m.flags.trail)
        for (let i = 0; i < n; i++) c.at(p.dash + i * iv, () => c.fixed(tot * NV(m, 'FC5C', 'ratio') / n * fire2(c, NV(m, 'FC5C', 'burn')), { label: '불타는 궤적', dot: true }));
      if (m.flags.aura)
        for (let i = 0; i < n; i++) c.at(p.dash + i * iv, () => c.fixed(tot * NV(m, 'FC10A', 'ratio') / n * fire2(c, NV(m, 'FC10A', 'burn')), { label: '불의 장막', dot: true }));
      if (m.flags.shock) c.at(p.dash, () => c.fixed(tot * NV(m, 'FC15C', 'ratio') * fire2(c, NV(m, 'FC15C', 'burn')), { label: '충격파' }));
      return p.dash;
    },
    combos: [
      { id: 'FCC', name: '화염 질주', requires: ['FC10C', 'FC15B'], desc: '자유 이동, 최대 스택 능력치 무효화, 사거리 증가', mods: { flags: { nullStacks: 1 } } },
    ],
    specials: [
      sp('FC5A', 5, '장거리 돌진', '사거리 +50%', null, NONE),
      sp('FC5B', 5, '강타', '적중 시 기절 +2초', { stunAdd: 2 }),
      sp('FC5C', 5, '불타는 궤적', '경로에 화염 장판 (총 피해량 200% 고정, 화상 상태 적 +100%)', { flags: { trail: 1 } }, null, { ratio: n_('총 피해량 비율', 2, '2 = 200%'), burn: n_('화상 상태 적 추가 배율', 1, '1 = +100%') }),
      sp('FC10A', 10, '불의 장막', '종료 시 불의 오오라 3초 (총 피해량 200% 고정, 화상 상태 적 +100%)', { flags: { aura: 1 } }, null, { ratio: n_('총 피해량 비율', 2, '2 = 200%'), burn: n_('화상 상태 적 추가 배율', 1, '1 = +100%') }),
      sp('FC10B', 10, '분쇄 돌파', '넉백 +50%, 적중 시 기절 +1초', { stunAdd: 1 }),
      sp('FC10C', 10, '연속 돌진', '최대 스택 +1, 쿨타임 -40%', { stacksAdd: 1, cdMul: 0.6 }),
      sp('FC15A', 15, '돌진 준비', '적중 시 남은 쿨타임 2.5초 감소', { hitCdSec: 2.5 }),
      sp('FC15B', 15, '무진돌파', '사거리 +100%, 적중 시 기절 +1초', { stunAdd: 1 }),
      sp('FC15C', 15, '충격파', '종료 시 충격파 (총 피해량 200% 고정, 화상 상태 적 +100%)', { flags: { shock: 1 } }, null, { ratio: n_('총 피해량 비율', 2, '2 = 200%'), burn: n_('화상 상태 적 추가 배율', 1, '1 = +100%') }),
    ] },

  { id: 'overslash', name: '오버 드라이브 플레임 슬래시', kind: 'manual', cd: 20,
    params: {
      blade: { l: '화염의 검 초기 피해량', v: 20 }, stun: { l: '기절 지속 시간', v: 2, u: '초' },
      expl: { l: '폭발 초기 피해량', v: 50 }, explDelay: { l: '폭발 딜레이 (내려친 뒤)', v: 1, u: '초' },
      windup: { l: '준비 시간', v: 0.35, u: '초' }, swing: { l: '내려치는 시간', v: 0.08, u: '초' },
    },
    cast(c, p, m) {
      const stunBase = p.stun + m.stunAdd;
      const focus = m.flags.focus ? 1 + NV(m, 'OS15A', 'base') + NV(m, 'OS15A', 'perSec') * stunBase : 1;   // 화염 집중: 최종 +100%, 무효화한 기절 1초당 +25%
      const tHit = p.windup + p.swing;
      const dl = Math.max(0, p.explDelay);
      c.at(tHit, () => c.hit(p.blade, { label: '화염의 검', stun: m.flags.focus ? 0 : stunBase, extraFinal: focus }));
      let end = tHit;
      if (!m.flags.focus) { c.at(tHit + dl, () => c.hit(p.expl, { label: '폭발' })); end = tHit + dl; }
      if (m.flags.trace) {   // 불타는 잔흔: 총 피해량 60% 고정 지속 피해 3초 (0.5초 간격)
        const total = c.A(p.blade, { extraFinal: focus }) + (m.flags.focus ? 0 : c.A(p.expl));
        const tk = Math.max(1, Math.round(NV(m, 'OS10A', 'time') / 0.5));
        for (let i = 0; i < tk; i++) c.at(end + 0.5 * i, () => c.fixed(total * NV(m, 'OS10A', 'ratio') / tk, { label: '불타는 잔흔', dot: true }));
        end += NV(m, 'OS10A', 'time');
      }
      return end;
    },
    combos: [
      { id: 'OSC', name: '오버 드라이브 블레이징 슬래시', requires: ['OS10B', 'OS15B'],
        desc: '추가 사용 무효화, 범위 +25%, 최종 피해량 +50% + 무효화한 추가 사용 1당 +25%',
        mods: { flags: { noExtras: 1 }, finalMul: 2 } },
    ],
    specials: [
      sp('OS5A', 5, '거대한 검', '범위 +50%, 피해량 +25%', { dmgPct: 0.25 }),
      sp('OS5B', 5, '옭아매는 화염', '기절 +2초, 피해량 +25%', { stunAdd: 2, dmgPct: 0.25 }),
      sp('OS5C', 5, '빠른 준비', '적중 시 남은 쿨타임 2.5% 감소', { hitCdRed: 0.025 }),
      sp('OS10A', 10, '불타는 잔흔', '종료 시 잔흔 3초 (총 피해량 60% 고정 지속 피해)', { flags: { trace: 1 } }, null, { ratio: n_('총 피해량 비율', 0.6), time: n_('지속 시간', 3, '초') }),
      sp('OS10B', 10, '붕괴의 일격', '기절 상태 적 적중 시 최종 피해량 +75%', { vsStun: 1.75 }),
      sp('OS10C', 10, '충격 집중', '기절 +2.5초, 쿨타임 감소 +20%, 피해량 +50%', { stunAdd: 2.5, cdMul: 0.8, dmgPct: 0.5 }),
      sp('OS15A', 15, '화염 집중', '폭발 삭제, 기절 무효화, 최종 +100% + 무효화한 기절 1초당 +25%', { flags: { focus: 1 } }, null, { base: n_('기본 최종 피해량', 1, '1 = +100%'), perSec: n_('무효화 1초당 최종 피해량', 0.25, '0.25 = +25%') }),
      sp('OS15B', 15, '삼연발', '양쪽 60도 간격으로 1회씩 추가 사용', { extras: [{ delay: 0, spread: 1 }, { delay: 0, spread: 1 }] }),
      sp('OS15C', 15, '후폭풍', '폭발 적중 시 적들을 중심으로 모음, 범위 +50%, 최종 +50%', { finalMul: 1.5 }),
    ] },

  { id: 'firebreaker', name: '파이어 브레이커', kind: 'manual', cd: 10,
    params: { dmg: { l: '폭발 초기 피해량', v: 10 }, count: { l: '폭발 횟수', v: 2 }, delay: { l: '폭발 사이 간격', v: 2, u: '초' }, stun: { l: '폭발당 기절', v: 1, u: '초' }, hotTime: { l: '불타는 대지 지속', v: 3, u: '초' } },
    cast(c, p, m) {
      let n = p.count + m.countAdd, nullified = 0;
      if (m.flags.gravity) { nullified = n - 1; n = 1; }   // 중력 붕괴: 공격 횟수 무효화 → 폭발 1회, 무효화 1당 기절 +2초
      const d = Math.max(0.25, p.delay * m.delayMul);
      for (let i = 0; i < n; i++) {
        const stun = p.stun + m.stunAdd + (m.flags.fissure ? NV(m, 'FBK5A', 'stunPer') * i : 0) + (m.flags.gravity ? NV(m, 'FBK15C', 'stunPerNull') * nullified : 0);
        c.at(i * d, () => {
          if (m.flags.rnd) {   // 조합: 기본 폭발 제거, 공격 횟수의 2배만큼 무작위 적 위치에 추가 폭발
            for (let k = 0; k < Math.round(n * NV(m, 'FKC', 'mult')); k++) c.hit(p.dmg, { label: '추가 폭발', stun, vuln: m.vuln, hits: Math.min(c.G.N, c.blast) });
          } else c.hit(p.dmg, { label: '폭발', stun, vuln: m.vuln });
        });
      }
      let end = (n - 1) * d;
      if (m.flags.hot) {   // 불타는 대지: 장판 위 화상 적의 스택 매 초 +1
        for (let i = 0; i < Math.floor(p.hotTime); i++) c.at(end + i, () => c.burnBoost(NV(m, 'FBK5B', 'stacks')));
        end += p.hotTime;
      }
      return end;
    },
    combos: [
      { id: 'FKC', name: '오버 드라이브 플레임 브레이커', requires: ['FBK10A', 'FBK15B'],
        desc: '기본 폭발 제거, 공격 횟수 +5, 최종 피해량 -60%, 공격 횟수의 2배만큼 무작위 적 위치에 추가 폭발',
        mods: { flags: { rnd: 1 }, countAdd: 5, finalMul: 0.4 },
        nums: { mult: n_('공격 횟수당 추가 폭발 수 (배수)', 2, '공격 횟수 × 이 값') } },
    ],
    specials: [
      sp('FBK5A', 5, '균열 확장', '폭발마다 범위 +50%, 기절 +1초', { flags: { fissure: 1 } }, null, { stunPer: n_('폭발마다 기절 증가', 1, '초') }),
      sp('FBK5B', 5, '불타는 대지', '종료 시 장판, 매 초 화상 스택 +1 · 이동 속도 -50%', { flags: { hot: 1 } }, null, { stacks: n_('매 초 화상 스택', 1) }),
      sp('FBK5C', 5, '빠른 폭발', '간격 -50%, 최종 피해량 +50%', { delayMul: 0.5, finalMul: 1.5 }),
      sp('FBK10A', 10, '연쇄 분출', '공격 횟수 +2', { countAdd: 2 }),
      sp('FBK10B', 10, '방출', '적중 시 넉백', null, NONE),
      sp('FBK10C', 10, '충격 흡수', '마지막 폭발 적중 시 적 하나당 최대 체력 5% 회복', null, NONE + ' (플레이어 체력은 없음)'),
      sp('FBK15A', 15, '중첩 붕괴', '적중 시 받는 최종 피해량 +20% (최대 5스택)', { vuln: { per: 0.2, max: 5, key: 'stack' } }),
      sp('FBK15B', 15, '지각 붕괴', '공격 횟수 +2, 간격 -50%', { countAdd: 2, delayMul: 0.5 }),
      sp('FBK15C', 15, '중력 붕괴', '공격 횟수 무효화 (폭발 1회), 무효화 1당 범위 +50% · 기절 +2초', { flags: { gravity: 1 } }, null, { stunPerNull: n_('무효화 1당 기절', 2, '초') }),
    ] },

  { id: 'fireexec', name: '파이어 엑스큐션', kind: 'manual', cd: 30,
    params: { dmg: { l: '베기 1회 초기 피해량', v: 5 }, count: { l: '공격 횟수', v: 30 }, interval: { l: '베기 간격', v: 0.025, u: '초' } },
    cast(c, p, m) {
      const dim = !!m.flags.dim;   // 플레임 디멘션 블레이드: 연속 베기 삭제, 검기만
      const n = p.count + m.countAdd;
      const iv = p.interval * m.intervalMul;
      const tot = c.A(p.dmg) * n;   // 총 피해량 = 최종 피해량 × 공격 횟수
      if (!dim) for (let i = 0; i < n; i++) c.at(i * iv, () => c.hit(p.dmg, { label: '베기' }));
      if (m.flags.wind && !dim) {   // 검풍: 베기 5회마다 검기 8개 (총 피해량 40% 를 개수로 나눠 고정)
        const ev = NV(m, 'FE10B', 'every'), wn = NV(m, 'FE10B', 'count');
        const volleys = Math.floor(n / ev), per = volleys > 0 ? tot * NV(m, 'FE10B', 'ratio') / (volleys * wn) : 0;
        for (let v = 1; v <= volleys; v++) c.at((v * ev - 1) * iv, () => { for (let k = 0; k < wn; k++) c.fixed(per, { label: '검풍', single: true, rate: c.G.projHit }); });
      }
      if (m.flags.burnBlast && !dim) {   // 타오르는 검: 화상 상태 적 5회 적중 시 폭발 (화상 총 피해량 = 1틱 × 스택 의 100% 고정)
        const be = NV(m, 'FE15C', 'every');
        for (let r = 1; r <= Math.floor(n / be); r++) c.at((r * be - 1) * iv, () => {
          if (c.burning()) c.fixed(c.burnTick() * NV(m, 'FE15C', 'ratio'), { label: '타오르는 검', dot: true, hits: Math.max(1, Math.round(c.G.N * c.G.burnShare)) * c.blast });
        });
      }
      const end = n * iv;
      if (m.flags.afterimage && !dim) {   // 잔영: 종료 시 소형 연속 베기 6개 (총 피해량 60% 고정, 8타씩)
        c.at(end, () => { const an = NV(m, 'FE15B', 'count') * NV(m, 'FE15B', 'ticks'); for (let s = 0; s < an; s++) c.fixed(tot * NV(m, 'FE15B', 'ratio') / an, { label: '잔영', hits: c.blast }); });
      }
      if (dim) {
        // 플레임 디멘션 블레이드: 검기 하나마다 (기존 총 피해량 40% 유도 검기) + (맞은 자리의 연속 베기 = 기존 총 피해량 100%) = 140%.
        // 총 피해량 = 최종 피해량 × 공격 횟수 (조합의 +30 이 먼저 적용된 횟수). 검기 수 = 공격 횟수 3당 1개 (나누지 않음)
        const ev = NV(m, 'FEC', 'every'), tk0 = NV(m, 'FEC', 'ticks'), tk = tk0 > 0 ? tk0 : n;   // 연속 베기 타수: 0 이면 공격 횟수 (조합의 +30 포함) 와 같음
        const proj = Math.floor(n / ev), T0 = c.A(p.dmg) * n;
        for (let k = 1; k <= proj; k++) c.at((k * ev - 1) * iv, () => {
          c.fixed(T0 * NV(m, 'FEC', 'waveRatio'), { label: '유도 검기', single: true, rate: 1 });
          for (let s = 0; s < tk; s++) c.at(iv * s, () => c.fixed(T0 * NV(m, 'FEC', 'slashRatio') / tk, { label: '검기 연속 베기', hits: c.blast }));   // 베기 1회 = 공격 간격마다
        });
      }
      return end;
    },
    combos: [
      { id: 'FEC', name: '플레임 디멘션 블레이드', requires: ['FE10B', 'FE15B'],
        desc: '연속 베기 삭제, 공격 횟수 +30, 공격 횟수 3당 유도 검기 1개 (유도 = 전부 적중), 검기가 적중한 위치에서 연속 베기가 공격 횟수만큼 (+30 포함) 일어남 → 검기 하나당 총 피해량의 40% + 100% = 140%',
        mods: { flags: { dim: 1 }, countAdd: 30 },
        nums: { every: n_('공격 횟수당 검기 1개', 3), waveRatio: n_('검기 총 피해량 비율', 0.4), slashRatio: n_('검기 적중 연속 베기 총 피해량 비율', 1), ticks: n_('연속 베기 타수 (0 = 공격 횟수와 같음)', 0) } },
    ],
    specials: [
      sp('FE5A', 5, '난도질', '공격 횟수 +15', { countAdd: 15 }),
      sp('FE5B', 5, '유동 검무', '이동 불가 삭제, 시전 중 이동 속도 -50%', null, NONE),
      sp('FE5C', 5, '집중 절삭', '범위 -50%, 피해량 +50%', { dmgPct: 0.5 }),
      sp('FE10A', 10, '고속 절단', '공격 시간 -75%, 쿨타임 감소 +25%', { intervalMul: 0.25, cdMul: 0.75 }),
      sp('FE10B', 10, '검풍', '베기 5회마다 검기 8개 (총 피해량 40% 고정)', { flags: { wind: 1 } }, '유도 없음: 명중률 = 전역 설정', { every: n_('베기 횟수마다', 5), count: n_('검기 수', 8), ratio: n_('총 피해량 비율', 0.4) }),
      sp('FE10C', 10, '작열 절삭', '화상 상태 적 적중 시 최종 피해량 +50%', { vsBurn: 1.5 }),
      sp('FE15A', 15, '무진참', '공격 횟수 +30, 쿨타임 +25%', { countAdd: 30, cdMul: 1.25 }),
      sp('FE15B', 15, '잔영', '종료 시 소형 연속 베기 6개 (총 피해량 60% 고정)', { flags: { afterimage: 1 } }, null, { count: n_('소형 연속 베기 수', 6), ticks: n_('연속 베기 타수', 8), ratio: n_('총 피해량 비율', 0.6) }),
      sp('FE15C', 15, '타오르는 검', '화상 상태 적 5회 적중 시 폭발 (화상 총 피해량 = 1틱 × 스택 의 100% 고정)', { flags: { burnBlast: 1 } }, null, { every: n_('적중 횟수마다', 5), ratio: n_('화상 총 피해량 비율', 1) }),
    ] },

  { id: 'firestream', name: '파이어 스트림', kind: 'move', cd: 8, stacks: 3,
    params: { dmg: { l: '초기 피해량', v: 3 }, dash: { l: '돌진 시간', v: 0.12, u: '초' }, fieldTime: { l: '장판 지속', v: 3, u: '초' }, lanceHit: { l: '화염의 창: 대기 중 피격 비율', v: 0.5, u: '0~1 (기대값으로 계산)' } },
    cast(c, p, m, u) {
      const lance = m.flags.lance ? p.lanceHit : 0;
      const lanceFinal = m.flags.lance ? 1 + NV(m, 'FST15A', 'final') * lance : 1;   // 화염의 창: 피격 시 최종 +400%
      const tot = c.A(p.dmg);
      c.at(p.dash * 0.5, () => c.hit(p.dmg, { label: u.extra ? '추가 사용' : '돌진', extraFinal: lanceFinal }));
      if (lance > 0 && !u.extra) c.at(p.dash * 0.5, () => c.reduceCdRatio(NV(m, 'FST15A', 'cdRed') * lance));
      if (m.flags.trail && !u.extra) {
        const iv = 0.5, n = Math.max(1, Math.round(p.fieldTime / iv));
        for (let i = 0; i < n; i++) c.at(p.dash + i * iv, () => c.fixed(tot * NV(m, 'FST5B', 'ratio') / n * fire2(c, NV(m, 'FST5B', 'burn')), { label: '불타는 궤적', dot: true }));
      }
      return p.dash;
    },
    combos: [
      { id: 'FTC', name: '화염 일섬', requires: ['FST10C', 'FST15A'],
        desc: '최종 피해량 +1000%, 쿨타임 감소 무효화 (1%당 +5%), 최대 스택 능력치 무효화 (1당 +150%)',
        mods: { nullCd: { base: 10, perPct: 0.05, perStack: 1.5 }, flags: { nullStacks: 1 } } },
    ],
    specials: [
      sp('FST5A', 5, '연속 돌진', '최대 스택 +1, 쿨타임 +20%', { stacksAdd: 1, cdMul: 1.2 }),
      sp('FST5B', 5, '불타는 궤적', '경로에 장판 (총 피해량 200% 고정, 화상 상태 적 +100%)', { flags: { trail: 1 } }, null, { ratio: n_('총 피해량 비율', 2, '2 = 200%'), burn: n_('화상 상태 적 추가 배율', 1, '1 = +100%') }),
      sp('FST5C', 5, '화염 장막', '종료 시 2초간 무적 유지', null, NONE),
      sp('FST10A', 10, '고속 질주', '사거리 +50%, 쿨타임 감소 +25%', { cdMul: 0.75 }),
      sp('FST10B', 10, '화염 환류', '적중 시 남은 쿨타임 2.5% 감소', { hitCdRed: 0.025 }),
      sp('FST10C', 10, '꿰뚫는 화염', '종료 시 진행 방향으로 추가 사용, 최종 +200%', { extras: [{ ratio: 1, final: 3, delay: 0.15 }] }),
      sp('FST15A', 15, '화염의 창', '피격 시 돌진: 최종 +400%, 남은 쿨타임 20% 감소', { flags: { lance: 1 } }, '피격 여부는 "대기 중 피격 비율" 기대값', { final: n_('피격 시 최종 피해량', 4, '4 = +400%'), cdRed: n_('피격 시 남은 쿨타임 감소', 0.2, '0.2 = 20%') }),
      sp('FST15B', 15, '후폭풍', '종료 시 지나온 경로에 추가 사용, 최종 +100%', { extras: [{ ratio: 1, final: 2, delay: 0.3 }] }),
      sp('FST15C', 15, '무한 기동', '최대 스택 +2, 쿨타임 감소 +50%, 최종 -75%', { stacksAdd: 2, cdMul: 0.5, finalMul: 0.25 }),
    ] },
];
