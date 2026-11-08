using UnityEngine;

public abstract class Weapon : MonoBehaviour
{
    public int level = 1;
    public const int MaxLevel = 8;
    protected float timer;

    public abstract string DisplayName { get; }
    /// <summary>다음 레벨(또는 신규 획득) 시 설명</summary>
    public abstract string NextDescription(int nextLevel);

    protected Player P => Player.I;

    public virtual void LevelUp() { level = Mathf.Min(level + 1, MaxLevel); }
    public abstract void Tick(float dt);

    protected static Enemy Nearest(Vector2 from, float maxDist)
    {
        Enemy best = null;
        float bestSq = maxDist * maxDist;
        foreach (var e in Enemy.All)
        {
            if (e.hp <= 0) continue;
            float sq = ((Vector2)e.transform.position - from).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = e; }
        }
        return best;
    }
}
