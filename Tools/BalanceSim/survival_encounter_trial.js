// [임시] 에른의 3택 1 성장과 몹 처치·경험치를 연결하는 생존 모드 비교. node survival_encounter_trial.js
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const here = __dirname;
const buildData = JSON.parse(fs.readFileSync(path.join(here, 'survival_skill_builds.json'), 'utf8'));
const html = fs.readFileSync(path.join(here, 'index.html'), 'utf8');
const section = (start, end) => html.slice(html.indexOf(`function ${start}(`), html.indexOf(`function ${end}(`));
const readJs = name => fs.readFileSync(path.join(here, name), 'utf8').replace(/\r\n/g, '\n');
// 파이어 슬래시는 Unity AfterFire 기준으로 1회 처리. 시뮬 원본은 수정하지 않는다.
const data = readJs('data.js').replace("        if (m.flags.quick) c.reduceCdRatio(NV(m, 'FSL10C', 'ratio'));\n", '');
const engine = readJs('sim.js')
  .replace('    return Math.min(90, (1 - Math.max(0.1, mul)) * 100);',
    "    if (sk.id === 'fireslash' && sk.m.nullCd && sk.m.flags.quick) return (1 - Math.max(0.1, mul) * (1 - NV(sk.m, 'FSL10C', 'ratio'))) * 100;\n    return Math.min(90, (1 - Math.max(0.1, mul)) * 100);")
  .replace('        sk.hitFactor = 1;\n      }\n    }\n  }',
    "        sk.hitFactor = 1;\n        if (sk.id === 'fireslash' && sk.m.flags.quick && !sk.m.nullCd) reduceCdRatio(sk, NV(sk.m, 'FSL10C', 'ratio'));\n      }\n    }\n  }");
if (engine === readJs('sim.js') || data === readJs('data.js')) throw new Error('빠른 공격 보정 위치 확인 필요');
const { SKILLS, PASSIVES, defaultState, makeCfg, runSim } = vm.runInNewContext([
  data, engine, section('defaultState', 'mergeState'), section('makeCfg', 'presetText'),
  '({ SKILLS, PASSIVES, defaultState, makeCfg, runSim })',
].join('\n'));

const stopAt = 750;
const step = .25;
const damageWindow = 30;
const seeds = [107, 241, 389, 521, 733, 947];
const roleGroups = Object.values(buildData.roleGroups);
const automatic = SKILLS.filter(skill => skill.kind === 'auto').map(skill => skill.id);
const passive = Object.keys(PASSIVES);
const pool = [...automatic, ...roleGroups.flat(), ...passive];
const enemyIds = ['goblin', 'archer', 'rider', 'hob'];
const enemyXp = [1, 2, 3, 5];
const currentMinInterval = .08;
const bossSchedule = [{ at: 180, hp: 1025, xp: 40 }, { at: 360, hp: 1550, xp: 40 }, { at: 600, hp: 4050, xp: 60 }];

// [임시] 대안은 체력, 물량, 종류 하나씩 변화시킨 뒤 최종 조합과 대조한다.
const plans = [
  { id: 'current-mobs', hp: [10, 8, 16, 60], floor: currentMinInterval, shift: 0 },
  { id: 'goblin15', hp: [15, 8, 16, 60], floor: currentMinInterval, shift: 0 },
  { id: 'less-spawns', hp: [15, 8, 16, 60], floor: .16, shift: 0 },
  { id: 'light-hob', hp: [15, 10, 20, 50], floor: currentMinInterval, shift: 0 },
  { id: 'more-ranged', hp: [15, 8, 16, 60], floor: currentMinInterval, shift: .05 },
  { id: 'combined', hp: [15, 10, 20, 50], floor: .16, shift: .05 },
  { id: 'combined-cap', hp: [15, 10, 20, 50], floor: .16, shift: .05, lateCap: 340 },
  { id: 'combined-cap-spawn08', hp: [15, 10, 20, 50], floor: currentMinInterval, shift: .05, lateCap: 340 },
  { id: 'combined-xp2', hp: [15, 10, 20, 50], floor: .16, shift: .05, xpAfterFirstBoss: [2, 4, 6, 10] },
  { id: 'combined-xp-high', hp: [15, 10, 20, 50], floor: .16, shift: .05, xpAfterFirstBoss: [3, 5, 7, 12] },
  { id: 'combined-cap-xp2', hp: [15, 10, 20, 50], floor: .16, shift: .05, lateCap: 340, xpAfterFirstBoss: [2, 4, 6, 10] },
];
const players = [
  { id: 'cautious', access: .36, uptime: .36, pickup: .70, approach: 9 },
  { id: 'reference', access: .65, uptime: .55, pickup: .82, approach: 8 },
  { id: 'grouping', access: .80, uptime: .72, pickup: .93, approach: 6 },
];
const routes = [
  { id: 'mixed', focus: ['solarslash', 'tripleslash', 'fs', 'fireball', 'overslash', 'firestream', 'hf'] },
  { id: 'automatic', focus: ['fireball', 'fireblade', 'solarslash', 'fs', 'hf', 'fireslash', 'firestorm'] },
  { id: 'boss', focus: ['tripleslash', 'hf', 'fireslash', 'firestream', 'overslash', 'fs'] },
  { id: 'burn', focus: ['fs', 'hf', 'fireball', 'solarslash', 'ab', 'firestorm'] },
  { id: 'control', focus: ['firebreaker', 'firecharge', 'firebuster', 'fireexec', 'fs', 'solarslash'] },
];
const rules = {
  ...buildData.expansionComparisons.skillRules,
  ...buildData.expansionComparisons.supportRules,
  fireball: { specials: ['FB5B', 'FB10A', 'FB15A'], upgrades: ['dmg'] },
  firebuster: { specials: ['FU5C', 'FU10C', 'FU15A'], upgrades: ['dmg'] },
  fireblade: { specials: ['FBL5A', 'FBL10B', 'FBL15A'], upgrades: ['dmg'] },
  firestorm: { specials: ['FS5B', 'FS10A', 'FS15B'], combo: 'FSC', upgrades: ['dmg'] },
  ab: { specials: ['H5A', 'H10A', 'H15A'], upgrades: ['fin', 'cd', 'stk'] },
};
const burnRules = {
  fs: { specials: ['B5B', 'B10B', 'B15A'], upgrades: ['dur', 'dps'] },
  hf: { specials: ['Y5A', 'Y10A', 'Y15B'], upgrades: ['dot', 'fin'] },
};
for (const id of pool) {
  const definition = SKILLS.find(s => s.id === id) || PASSIVES[id];
  if (!rules[id]?.specials.every((k, i) => definition.specials.some(s => s.id === k && s.lv === (i + 1) * 5)))
    throw new Error('특수 강화 ID 확인 필요: ' + id);
}

function seeded(seed) {
  let state = seed >>> 0;
  return () => { state ^= state << 13; state ^= state >>> 17; state ^= state << 5; return (state >>> 0) / 4294967296; };
}

function pickUpgrade(levels, route, roll) {
  const available = pool.filter(id => levels[id] < 15 && !roleGroups.some(group =>
    group.includes(id) && group.some(other => other !== id && levels[other] > 0)));
  const options = [];
  for (let i = 0; i < 3 && available.length; i++) options.push(available.splice(Math.floor(roll() * available.length), 1)[0]);
  const firstUnlock = route.focus.slice(0, 3).find(id => levels[id] === 0 && options.includes(id));
  const choice = firstUnlock || route.focus.find(id => options.includes(id)) || options[0];
  if (choice) levels[choice]++;
}

const powerCache = new Map();
function powerAt(levels, route, targetCount) {
  const key = [route.id, targetCount, ...pool.map(id => levels[id])].join('/');
  if (powerCache.has(key)) return powerCache.get(key);
  const state = defaultState();
  Object.assign(state.G, { T: damageWindow, N: targetCount, boss: targetCount === 1 ? 1 : 0, enemyHp: 100, killRate: 0 });
  function spec(id) {
    const rule = route.id === 'burn' && burnRules[id] ? burnRules[id] : rules[id];
    const ups = { dmg: 0, cd: 0 };
    for (let i = 0; i < levels[id] - 1; i++) {
      const stat = rule.upgrades[i % rule.upgrades.length]; ups[stat] = (ups[stat] || 0) + 1;
    }
    const picks = Object.fromEntries([5, 10, 15].flatMap((lv, i) => levels[id] >= lv ? [[lv, rule.specials[i]]] : []));
    return { on: true, ups, picks, combo: levels[id] >= 15 ? rule.combo || null : null };
  }
  for (const skill of state.skills) { skill.on = levels[skill.id] > 0; if (skill.on) Object.assign(skill, spec(skill.id)); }
  for (const [id, p] of Object.entries(state.pas)) { p.on = levels[id] > 0; if (p.on) Object.assign(p, spec(id)); }
  const r = runSim(makeCfg(state));
  // 짧은 일반 몹 생존 시간에 맞춰, 30초 고정 대상의 화상·폭발 피해를 그대로 이월하지 않는다.
  const power = (r.total - r.passive.total * (targetCount === 1 ? 0 : .75)) / r.T;
  powerCache.set(key, power);
  return power;
}

function xpToNext(level) {
  const base = 5 + (level - 1) * 6 + Math.floor(level / 10) * 20;
  if (level < 10) return Math.max(5, Math.round(base * .85));
  if (level < 20) return base;
  return Math.round(base * (level < 60 ? .90 : 1.15));
}

function distribution(minute, shift) {
  const h = minute > 2 ? Math.min(.2, .05 + (minute - 2) * .03) : 0;
  const r = minute > 1.5 ? Math.min(.2, .08 + (minute - 1.5) * .03) : 0;
  const a = minute > .75 ? Math.min(.2, .1 + (minute - .75) * .03) : 0;
  const later = minute >= 2 ? shift : 0;
  return [1 - h - r - a - 2 * later, a + later, r + later, h];
}

function simulate(plan, player, route, seed) {
  const mobs = seeded(seed), choose = seeded(seed ^ 0x17ab3929), pickup = seeded(seed ^ 0x91ca4f13);
  const levels = Object.fromEntries(pool.map(id => [id, id === 'fireslash' ? 1 : 0]));
  const enemies = [], drops = [], bosses = [], samples = [];
  const spawnedByKind = [0, 0, 0, 0];
  let head = 0, dropHead = 0, spawnCredit = 0, damageCredit = 0;
  let level = 1, totalXp = 0, nextXp = xpToNext(1);
  let single = powerAt(levels, route, 1), group = single, updatePower = true, peakAlive = 0;
  for (let time = step; time <= stopAt; time += step) {
    const min = time / 60;
    if (time < 600) {
      const interval = Math.max(plan.floor, .9 - min * .12);
      const batch = 1 + Math.trunc(min * .8), cap = Math.min(plan.lateCap ?? Infinity, 60 + Math.trunc(min * 40));
      spawnCredit = Math.min(1 + step / interval, spawnCredit + step / interval);
      const ratios = distribution(min, plan.shift);
      while (spawnCredit >= 1 && enemies.length - head < cap) {
        spawnCredit--;
        for (let i = 0; i < batch; i++) {
          const roll = mobs();
          let kind = 0, cumulative = ratios[0];
          while (kind < ratios.length - 1 && roll >= cumulative) cumulative += ratios[++kind];
          spawnedByKind[kind]++;
          const xpByKind = time >= 180 && plan.xpAfterFirstBoss ? plan.xpAfterFirstBoss : enemyXp;
          enemies.push({ hp: plan.hp[kind] * (1 + .35 * min), xp: xpByKind[kind], ready: time + player.approach });
        }
        peakAlive = Math.max(peakAlive, enemies.length - head);
      }
    }
    const slot = bossSchedule.findIndex(b => b.at === time);
    if (slot !== -1) bosses.push({ ...bossSchedule[slot], hpLeft: bossSchedule[slot].hp, entered: level, overlap: bosses.some(b => b.hpLeft > 0), killedAt: null });
    if (updatePower) { single = powerAt(levels, route, 1); group = powerAt(levels, route, 5); updatePower = false; }
    const boss = bosses.find(b => b.hpLeft > 0), bossShare = boss ? (route.id === 'boss' ? .80 : .6) : 0;
    if (boss) {
      boss.hpLeft -= step * player.uptime * single * bossShare;
      if (boss.hpLeft <= 0) { boss.killedAt = time; drops.push({ at: time + 2, xp: boss.xp }); }
    }
    const ready = enemies[head]?.ready <= time;
    if (ready) {
      const crowded = enemies[head + 4]?.ready <= time;
      const usable = crowded ? group : single;
      damageCredit = Math.min(usable, damageCredit + step * player.access * usable * (1 - bossShare));
      while (enemies[head]?.ready <= time && damageCredit >= enemies[head].hp) {
        const e = enemies[head++]; damageCredit -= e.hp;
        if (pickup() < player.pickup) drops.push({ at: time + 2, xp: e.xp });
      }
    } else damageCredit = 0;
    while (drops[dropHead]?.at <= time) totalXp += drops[dropHead++].xp;
    while (totalXp >= nextXp && level < 150) {
      level++; nextXp += xpToNext(level); pickUpgrade(levels, route, choose); updatePower = true;
    }
    if ([180, 360, 600].includes(time)) samples.push({ time, level, xp: totalXp, alive: enemies.length - head, killed: head });
    if (bosses[2]?.killedAt != null) break;
  }
  const final = bosses[2];
  return { level, xp: totalXp, peakAlive, samples, spawnedByKind, bosses,
    skills: Object.fromEntries(Object.entries(levels).filter(([, lv]) => lv > 0)),
    clear: final.killedAt != null, inBand: final.killedAt != null && level >= 55 && level <= 65 };
}

const mean = values => values.length ? +(values.reduce((a, b) => a + b, 0) / values.length).toFixed(2) : null;
const runs = [];
for (const plan of plans) for (const player of players) for (const route of routes) for (const seed of seeds) {
  runs.push({ plan: plan.id, player: player.id, route: route.id, seed, ...simulate(plan, player, route, seed) });
}
const summary = plans.map(plan => {
  const group = runs.filter(r => r.plan === plan.id);
  return { plan: plan.id, players: players.map(player => {
    const rows = group.filter(r => r.player === player.id), cleared = rows.filter(r => r.clear);
    return { player: player.id, runs: rows.length, cleared: cleared.length, inBand: rows.filter(r => r.inBand).length,
      milestoneLevels: [0, 1, 2].map(i => mean(rows.map(r => r.samples[i].level))), clearLevel: mean(cleared.map(r => r.level)),
      finalBossSeconds: mean(cleared.map(r => r.bosses[2].killedAt - 600)), peakAlive: mean(rows.map(r => r.peakAlive)),
      spawnedByKind: enemyIds.map((kind, i) => ({ kind, mean: mean(rows.map(r => r.spawnedByKind[i])) })),
      overlapAt10: rows.filter(r => r.bosses[2].overlap).length };
  }) };
});
process.stdout.write(JSON.stringify({ plans, players, routes, seeds, bossSchedule, stopAt,
  pool: { automatic, passive, roleGroups },
  assumptions: { spawnStopsAt: 600, oldestBossTargetedFirst: true, playerDeathModeled: false, crowdPassiveRetention: .25 },
  summary, runs }) + '\n');
