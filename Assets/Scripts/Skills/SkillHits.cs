using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 판정 공용 함수. 모두 Enemy.CopyAll 사본으로 돌기 때문에 순회 중 적이 죽어도 안전.
/// onHit 은 피해를 준 직후 호출 (기절·슬로우 부여 등).
/// </summary>
public static class SkillHits
{
    static readonly List<Enemy> buf = new List<Enemy>();
    static readonly List<Enemy> result = new List<Enemy>();

    /// <summary>원 안의 살아 있는 적 목록 (피해 없이 조회만)</summary>
    public static List<Enemy> InCircle(Vector2 c, float r, List<Enemy> into)
    {
        into.Clear();
        foreach (var e in Enemy.CopyAll(buf))
        {
            if (e == null || e.hp <= 0) continue;
            float rr = r + e.radius;
            if (((Vector2)e.transform.position - c).sqrMagnitude < rr * rr) into.Add(e);
        }
        return into;
    }

    /// <summary>원형 범위 피해. 밀치기는 중심에서 바깥 방향</summary>
    public static int Circle(Vector2 c, float r, float dmg, float knock, Action<Enemy> onHit = null)
    {
        InCircle(c, r, result);
        foreach (var e in result)
        {
            Vector2 d = (Vector2)e.transform.position - c;
            e.Hit(dmg, d.sqrMagnitude > 0.0001f ? d : UnityEngine.Random.insideUnitCircle, knock);
            onHit?.Invoke(e);
        }
        return result.Count;
    }

    /// <summary>부채꼴 범위 피해 (dir 기준 좌우 halfAngle 도)</summary>
    public static int Sector(Vector2 c, Vector2 dir, float r, float halfAngle, float dmg, float knock, Action<Enemy> onHit = null)
    {
        dir = dir.normalized;
        float cosLimit = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        int n = 0;
        foreach (var e in Enemy.CopyAll(buf))
        {
            if (e == null || e.hp <= 0) continue;
            Vector2 d = (Vector2)e.transform.position - c;
            float rr = r + e.radius;
            if (d.sqrMagnitude > rr * rr) continue;
            // 아주 가까운 적은 각도와 관계없이 맞음
            if (d.magnitude > e.radius + 0.3f && Vector2.Dot(d.normalized, dir) < cosLimit) continue;
            e.Hit(dmg, d.sqrMagnitude > 0.0001f ? d : dir, knock);
            onHit?.Invoke(e);
            n++;
        }
        return n;
    }

    /// <summary>origin 에서 dir 방향으로 length, 폭 width 인 직사각형 범위 피해</summary>
    public static int Box(Vector2 origin, Vector2 dir, float length, float width, float dmg, float knock, Action<Enemy> onHit = null)
    {
        dir = dir.normalized;
        var side = new Vector2(-dir.y, dir.x);
        int n = 0;
        foreach (var e in Enemy.CopyAll(buf))
        {
            if (e == null || e.hp <= 0) continue;
            Vector2 d = (Vector2)e.transform.position - origin;
            float along = Vector2.Dot(d, dir), across = Mathf.Abs(Vector2.Dot(d, side));
            if (along < -e.radius || along > length + e.radius || across > width * 0.5f + e.radius) continue;
            e.Hit(dmg, dir, knock);
            onHit?.Invoke(e);
            n++;
        }
        return n;
    }

    /// <summary>카메라에 보이는 모든 적</summary>
    public static List<Enemy> OnScreen(List<Enemy> into)
    {
        into.Clear();
        var cam = Camera.main;
        if (cam == null) return into;
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        foreach (var e in Enemy.CopyAll(buf))
        {
            if (e == null || e.hp <= 0) continue;
            Vector2 d = (Vector2)e.transform.position - c;
            if (Mathf.Abs(d.x) <= w + e.radius && Mathf.Abs(d.y) <= h + e.radius) into.Add(e);
        }
        return into;
    }
}
