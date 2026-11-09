using UnityEngine;

/// <summary>
/// 캐릭터별 감각 조정 값. (예: Assets/Resources/Config/ErnStrayConfig.asset)
/// Play 중에 Inspector 에서 바꾸면 즉시 반영되고, Play 를 멈춰도 값이 유지됩니다.
/// </summary>
[CreateAssetMenu(menuName = "Slayers/Player Config", fileName = "PlayerConfig")]
public class PlayerConfig : ScriptableObject
{
    [Header("피격 판정 (초록 원)")]
    [Tooltip("적에게 닿으면 피해를 입는 반경")]
    [Range(0.05f, 2f)] public float hurtRadius = 0.4f;
    [Tooltip("판정 중심을 캐릭터 위치에서 얼마나 옮길지")]
    public Vector2 hurtOffset = Vector2.zero;

    [Header("획득 범위 (하늘색 원)")]
    [Tooltip("경험치 보석을 끌어오기 시작하는 반경 (업그레이드 배율이 곱해짐)")]
    [Range(0.2f, 8f)] public float pickupRadius = 2f;

    [Header("이동")]
    [Tooltip("기본 이동 속도 (업그레이드 배율이 곱해짐)")]
    [Range(0.5f, 12f)] public float moveSpeed = 4.5f;

    [Header("치명타")]
    [Range(0f, 1f)] public float critChance = 0.5f;

    [Header("공격 모션")]
    [Tooltip("공격 모션 재생 속도 (8프레임 기준, 16 = 0.5초)")]
    [Range(4f, 40f)] public float attackFps = 16f;

    [Header("기본 공격 이펙트")]
    [Tooltip("모션 시작 후 이펙트가 나오기까지 (초)")]
    [Range(0f, 0.5f)] public float fxDelay = 0.125f;
    [Tooltip("캐릭터에서 바라보는 방향으로 떨어진 거리")]
    [Range(0f, 2f)] public float fxDistance = 0.7f;
    [Tooltip("위치 미세 조정 (오른쪽을 볼 때 기준, 방향에 맞춰 회전)")]
    public Vector2 fxOffset = Vector2.zero;
    [Range(0.3f, 5f)] public float fxSize = 2.0f;
    [Range(8f, 60f)] public float fxFps = 32f;
}
