using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 둘레를 반시계 방향으로 도는 불꽃 줄기 파티클 (2026-11-21 사용자: 블레이징 스톰 마지막 동작에서 검의 경로를 따라 움직이는 느낌).
/// 불꽃 한 장 그림 (머리 = 오른쪽, 꼬리 = 왼쪽) 을 타원 궤도의 접선 방향으로 눕혀 머리가 진행 방향을 향하게 흩뿌림.
/// 발사 지점은 sweepStart 도에서 반시계로 sweepDegrees 도만 한 번 훑고 끝남 (2026-11-21: 7시에서 시작해 한 바퀴 돌아 8시쯤에서 끝, 각도는 3시 = 0도 · 12시 = 90도 · 반시계가 +).
/// 줄기는 작게 많이 생기고 금방 사라져서 불티가 검의 경로를 따라 번져 나가는 모양이 됨.
/// 타원 아래쪽 (앞) = 플레이어 앞 순서 · 위쪽 (뒤) = 플레이어 뒤 순서 + 어둡게 (토네이도 리본과 같은 규칙). 실제 시간으로 움직임 (WorldSlow 와 무관).
/// </summary>
public class OrbitFlameFx : MonoBehaviour
{
    public class Settings
    {
        public Sprite sprite;               // 줄기 그림 (머리 = 오른쪽)
        public float emitSeconds = 1.3f;    // 발사하는 시간
        public float sweepSeconds = 0f;     // 발사 지점이 시작 → 끝 각도를 훑는 데 걸리는 시간 (0 이하면 emitSeconds). emitSeconds 가 더 짧으면 훑는 중간에 발사를 멈춤 (남은 구간은 다른 파티클이 채움)
        public float rate = 45f;            // 초당 발사 수
        public float life = 0.5f;           // 줄기 하나가 남는 시간 (lifeMid / lifeLate 가 0 이하면 전부 이 값)
        public float lifeMid = 0f, lifeLate = 0f;   // 발사 시간의 중반 (1/3~2/3) · 후반 (2/3~) 에 나오는 줄기의 수명. life = 초반
        public float radiusMin = 1.2f, radiusMax = 2.6f;   // 플레이어 중심에서의 거리 (월드)
        public float spread = 0f;           // 수명 동안 바깥쪽으로 더 나가는 거리 (월드). 0 이면 궤도 접선 방향, 0 보다 크면 바깥 방향으로 퍼짐
        public float spreadRandom = 0f;     // 퍼지는 거리의 무작위 폭 (0.5 = 0.5~1.5 배)
        public float squash = 0.5f;         // 타원 세로 / 가로 (위에서 본 원을 옆에서 본 납작함)
        public float omegaMin = 420f, omegaMax = 600f;     // 회전 속도 (도/초, 반시계)
        public float sweepStart = 240f;     // 발사 지점의 시작 각도 (도). 7시 = 240, 3시 = 0, 12시 = 90, 9시 = 180
        public float sweepDegrees = 330f;   // 발사 지점이 반시계로 훑는 각도 (도). 240 에서 330 → 570 = 210 (8시)
        public float emitterJitter = 30f;   // 발사 각도 흔들림 (±도)
        public float scaleMin = 0.8f, scaleMax = 1.3f;     // 크기 배율
        public int backOrder = 9, frontOrder = 11;         // 플레이어 = 10
        public Color backTint = new Color(0.6f, 0.4f, 0.35f, 0.7f);
    }

    class P
    {
        public Transform tf; public SpriteRenderer sr;
        public float angle, radius, omega, scale, born, life, spreadMul;
    }

    Settings s;
    float t, emitAcc, emitAngle;
    readonly List<P> live = new List<P>();
    readonly Stack<P> pool = new Stack<P>();

    public static OrbitFlameFx Spawn(Settings settings, Transform parent)
    {
        if (settings == null || settings.sprite == null || parent == null) return null;
        var go = new GameObject("FX_OrbitFlame");
        go.transform.SetParent(parent, false);
        var fx = go.AddComponent<OrbitFlameFx>();
        fx.s = settings;
        fx.emitAngle = settings.sweepStart;
        fx.emitAcc = 1f;   // 시작하자마자 첫 파티클
        return fx;
    }

    void Update()
    {
        float dt = WorldSlow.EffectDelta;
        t += dt;

        // 발사 (반시계로 도는 발사 지점)
        if (t < s.emitSeconds)
        {
            emitAngle = s.sweepStart + s.sweepDegrees * Mathf.Clamp01(t / (s.sweepSeconds > 0f ? s.sweepSeconds : s.emitSeconds));   // 시작 → 끝 한 번만 반시계로
            emitAcc += s.rate * dt;
            while (emitAcc >= 1f) { emitAcc -= 1f; Emit(); }
        }

        for (int i = live.Count - 1; i >= 0; i--)
        {
            var p = live[i];
            float k = (t - p.born) / p.life;
            if (k >= 1f) { p.sr.enabled = false; pool.Push(p); live.RemoveAt(i); continue; }
            p.angle += p.omega * dt;
            Place(p, k);
        }
        if (t >= s.emitSeconds && live.Count == 0) Destroy(gameObject);
    }

    void Emit()
    {
        P p = pool.Count > 0 ? pool.Pop() : NewParticle();
        p.angle = emitAngle + Random.Range(-s.emitterJitter, s.emitterJitter);
        p.radius = Random.Range(s.radiusMin, s.radiusMax);
        p.spreadMul = 1f + Random.Range(-s.spreadRandom, s.spreadRandom);
        p.omega = Random.Range(s.omegaMin, s.omegaMax);
        p.scale = Random.Range(s.scaleMin, s.scaleMax);
        p.born = t;
        float prog = t / (s.sweepSeconds > 0f ? s.sweepSeconds : s.emitSeconds);   // 초반 · 중반 · 후반에 따라 수명이 다름
        p.life = (s.lifeMid > 0f && prog >= 1f / 3f) ? ((s.lifeLate > 0f && prog >= 2f / 3f) ? s.lifeLate : s.lifeMid) : s.life;
        p.sr.enabled = true;
        live.Add(p);
    }

    P NewParticle()
    {
        var go = new GameObject("p");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s.sprite;
        sr.enabled = false;
        return new P { tf = go.transform, sr = sr };
    }

    void Place(P p, float k)
    {
        float a = p.angle * Mathf.Deg2Rad;
        // 타원 궤도 위의 위치와 진행 방향 (반시계 = 각도 증가 쪽)
        float r = p.radius + s.spread * p.spreadMul * k;   // 바깥으로 퍼짐
        Vector2 pos = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * s.squash);
        Vector2 tangent = s.spread > 0f
            ? new Vector2(Mathf.Cos(a), Mathf.Sin(a) * s.squash)    // 바깥 방향 (머리 = 바깥쪽)
            : new Vector2(-Mathf.Sin(a), Mathf.Cos(a) * s.squash);  // 접선 (반시계)
        p.tf.localPosition = pos;
        p.tf.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);   // 머리 (오른쪽) 가 진행 방향
        p.tf.localScale = Vector3.one * p.scale;

        bool front = Mathf.Sin(a) < 0f;   // 타원 아래쪽 = 앞
        float depth = (1f - Mathf.Sin(a)) * 0.5f;
        var col = Color.Lerp(s.backTint, Color.white, depth);
        col.a *= Mathf.Sin(Mathf.PI * Mathf.Clamp01(k));   // 나타났다 사라짐
        p.sr.color = col;
        p.sr.sortingOrder = front ? s.frontOrder : s.backOrder;
    }
}
