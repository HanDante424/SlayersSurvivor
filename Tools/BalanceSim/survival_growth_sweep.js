// Unity의 10분 생존 스폰·경험치 규칙을 쓰는 [임시] 성장 비교 모델. node survival_growth_sweep.js
const duration = 600;
const framesPerSecond = 120;
const checkpoints = [60, 180, 360, 420, 540, 600];
const milestones = [2, 5, 10, 15, 20, 30, 45, 60, 70];
const seeds = [107, 241, 389, 521, 733, 947];

// [임시] 범위·명중·초과 피해를 반영했다고 가정한 유효 DPS. 배열은 0·60·180·360초 기준.
const players = [
  { id: 'slow', clear: .40, pickup: .65, approach: 10, collect: 4, dps: [6, 24, 120, 1400] },
  { id: 'careful', clear: .55, pickup: .75, approach: 9, collect: 3, dps: [8, 32, 170, 1600] },
  { id: 'reference', clear: .65, pickup: .80, approach: 8, collect: 2, dps: [10, 40, 225, 1800] },
  { id: 'strong', clear: .80, pickup: .90, approach: 7, collect: 2, dps: [13, 52, 300, 2000] },
  { id: 'top', clear: .90, pickup: .95, approach: 7, collect: 1, dps: [16, 65, 400, 2172.156] },
];
const encounters = [
  { id: 'current', goblinHp: 10, minInterval: .08, lateHp: 0 },
  { id: 'goblin15', goblinHp: 15, minInterval: .08, lateHp: 0 },
  { id: 'goblin15-spawn12', goblinHp: 15, minInterval: .12, lateHp: 0 },
  { id: 'goblin15-spawn16', goblinHp: 15, minInterval: .16, lateHp: 0 },
  { id: 'goblin15-lateHp20', goblinHp: 15, minInterval: .08, lateHp: .20 },
];
const curves = [
  { id: 'current', early: 1, late: 1, after60: 1 },
  { id: 'early15', early: .85, late: 1, after60: 1 },
  { id: 'early15-late10', early: .85, late: .90, after60: .90 },
  { id: 'early15-late10-after60', early: .85, late: .90, after60: 1.15 },
];

function randomSequence(seed) {
  let x = seed >>> 0;
  return () => {
    x ^= x << 13;
    x ^= x >>> 17;
    x ^= x << 5;
    return (x >>> 0) / 0x100000000;
  };
}

function spawnPlan(second, minimum) {
  const minute = second / 60;
  return {
    interval: Math.max(minimum, .9 - minute * .12),
    batch: 1 + Math.trunc(minute * .8),
    cap: 60 + Math.trunc(minute * 40),
  };
}

function newEnemy(second, encounter, roll) {
  const minute = second / 60;
  const hob = minute > 2 ? Math.min(.2, .05 + (minute - 2) * .03) : 0;
  const rider = minute > 1.5 ? Math.min(.2, .08 + (minute - 1.5) * .03) : 0;
  const archer = minute > .75 ? Math.min(.2, .1 + (minute - .75) * .03) : 0;
  const hpMultiplier = (1 + minute * .35) * (1 + encounter.lateHp * Math.max(0, (second - 360) / 240));
  if (roll < hob) return { hp: 60 * hpMultiplier, xp: 5 };
  if (roll < hob + rider) return { hp: 16 * hpMultiplier, xp: 3 };
  if (roll < hob + rider + archer) return { hp: 8 * hpMultiplier, xp: 2 };
  return { hp: encounter.goblinHp * hpMultiplier, xp: 1 };
}

function xpToNext(level, curve) {
  const base = 5 + (level - 1) * 6 + Math.floor(level / 10) * 20;
  if (level < 10) return Math.max(5, Math.round(base * curve.early));
  if (level < 20) return base;
  return Math.round(base * (level < 60 ? curve.late : curve.after60));
}

function effectiveDps(second, profile) {
  const anchors = [0, 60, 180, 360];
  for (let i = 1; i < anchors.length; i++) {
    if (second >= anchors[i]) continue;
    const fraction = (second - anchors[i - 1]) / (anchors[i] - anchors[i - 1]);
    return profile.dps[i - 1] + (profile.dps[i] - profile.dps[i - 1]) * fraction;
  }
  return profile.dps.at(-1);
}

function combatRun(player, encounter, seed) {
  const spawnRoll = randomSequence(seed);
  const pickupRoll = randomSequence(seed ^ 0x95ba32f1);
  const enemies = [], drops = [], samples = [];
  let enemyHead = 0, dropHead = 0, nextSpawn = 0, nextDemand = 0;
  let demand = 0, damage = 0, xp = 0, peakAlive = 0, cappedFrames = 0;
  for (let frame = 1; frame <= duration * framesPerSecond; frame++) {
    const second = frame / framesPerSecond;
    if (second < duration) {
      const plan = spawnPlan(second, encounter.minInterval);
      if (second >= nextSpawn) {
        if (enemies.length - enemyHead < plan.cap) {
          nextSpawn = second + plan.interval;
          for (let i = 0; i < plan.batch; i++) {
            enemies.push({ ...newEnemy(second, encounter, spawnRoll()), ready: second + player.approach });
          }
          peakAlive = Math.max(peakAlive, enemies.length - enemyHead);
        } else cappedFrames++;
      }
      if (second >= nextDemand) {
        nextDemand = second + plan.interval;
        demand = Math.min(1 + plan.batch, demand + plan.batch * player.clear);
      }
      const dps = effectiveDps(second, player);
      // 아직 다가오지 않은 적을 상대로 피해를 미리 쌓지 않는다. 피해는 먼저 접근한 적부터 처리한다.
      if (enemies[enemyHead]?.ready <= second) damage = Math.min(dps * 3, damage + dps / framesPerSecond);
      else damage = 0;
      while (demand >= 1 && enemies[enemyHead]?.ready <= second && damage >= enemies[enemyHead].hp) {
        const enemy = enemies[enemyHead++];
        damage -= enemy.hp;
        demand -= 1;
        if (pickupRoll() < player.pickup) drops.push({ xp: enemy.xp, time: second + player.collect });
      }
    }
    while (dropHead < drops.length && drops[dropHead].time <= second) {
      xp += drops[dropHead].xp;
      dropHead++;
    }
    samples.push(xp);
    if (checkpoints.includes(second)) samples[frame - 1] = {
      xp, spawned: enemies.length, killed: enemyHead, alive: enemies.length - enemyHead,
    };
  }
  return { samples, peakAlive, capSeconds: cappedFrames / framesPerSecond };
}

function evaluateCurve(run, curve) {
  let level = 1, required = xpToNext(level, curve);
  const times = {}, timeline = [];
  run.samples.forEach((sample, i) => {
    const xp = typeof sample === 'number' ? sample : sample.xp;
    const second = (i + 1) / framesPerSecond;
    while (xp >= required) {
      level++;
      required += xpToNext(level, curve);
      if (milestones.includes(level)) times[level] = second;
    }
    if (typeof sample !== 'number') timeline.push({ second, level, ...sample });
  });
  return { times, timeline, peakAlive: run.peakAlive, capSeconds: run.capSeconds };
}

const average = values => values.reduce((a, b) => a + b, 0) / values.length;
const round = value => Number(value.toFixed(2));
function summarize(repeats) {
  return {
    timeline: checkpoints.map((second, i) => {
      const points = repeats.map(r => r.timeline[i]);
      const stats = ['level', 'xp', 'spawned', 'killed', 'alive'];
      return { second, ...Object.fromEntries(stats.map(key => [key, round(average(points.map(p => p[key])))])) };
    }),
    milestones: Object.fromEntries(milestones.map(level => {
      const times = repeats.flatMap(r => r.times[level] == null ? [] : [r.times[level]]);
      return [level, { reached: times.length, averageSecond: times.length ? round(average(times)) : null }];
    })),
    endingLevelRange: [Math.min(...repeats.map(r => r.timeline.at(-1).level)), Math.max(...repeats.map(r => r.timeline.at(-1).level))],
    peakAlive: round(average(repeats.map(r => r.peakAlive))),
    blockedSpawnSeconds: round(average(repeats.map(r => r.capSeconds))),
  };
}

const results = [];
for (const encounter of encounters) for (const player of players) {
  // XP 곡선에 따른 피해 증가량은 실측 전이므로, 같은 전투 결과로 XP 곡선만 비교한다.
  const runs = seeds.map(seed => combatRun(player, encounter, seed));
  for (const curve of curves) {
    results.push({ encounter: encounter.id, player: player.id, curve: curve.id,
      ...summarize(runs.map(run => evaluateCurve(run, curve))) });
  }
}

process.stdout.write(JSON.stringify({ duration, framesPerSecond, seeds, players, encounters, curves, results }) + '\n');
