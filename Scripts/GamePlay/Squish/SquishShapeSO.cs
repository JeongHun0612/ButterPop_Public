using System;
using UnityEngine;

// 스퀴시 오브젝트의 "형태 정의". 스프라이트 실루엣 하나당 하나 authoring한다.
// - controlPoints: 끌대(스파인) 중심선 + 국소 폭을 저해상도로 찍은 제어점. sprite-local 공간.
// - 빌더(SquishBody/SquishMesh)가 이걸 호 길이로 리샘플링해 물리 뼈 / 비주얼 격자를 뽑는다.
// - 같은 실루엣을 공유하는 스킨끼리 이 SO 하나를 재사용한다.
[CreateAssetMenu(fileName = "SquishShape", menuName = "BananaGallery/Squish/Squish Shape")]
public class SquishShapeSO : ScriptableObject
{
    // 스파인 제어점 하나: 위치 + 그 지점의 반폭(중심선에서 표면까지 거리).
    [Serializable]
    public struct SpinePoint
    {
        public Vector2 position;
        public float halfWidth;
    }

    [Header("스프라이트")]
    public Sprite sprite;
    [Tooltip("스프라이트 rect 내 실제 불투명(아트) 영역, 0~1. 저작/시드 시 자동 계산. 메쉬 bbox를 이 영역에 매핑해 여백 스프라이트도 정확히 정합.")]
    [HideInInspector] public Rect opaqueUVRect = new Rect(0f, 0f, 1f, 1f);

    [Header("스파인 (끌대 중심선) — sprite-local, 순서대로")]
    public SpinePoint[] controlPoints;
    [Tooltip("제어점 사이를 Catmull-Rom으로 매끄럽게 이을지. 끄면 직선 연결.")]
    public bool smoothSpine = true;

    [Header("물리 뼈")]
    [Tooltip("스파인을 리샘플링해 만들 강체 세그먼트 수 (9~11 권장)")]
    [Range(2, 32)]
    public int boneCount = 11;

    [Header("질량 / 중력 / 물성")]
    public float totalMass = 1.2f;
    public float gravityScale = 1.5f;
    [Range(0f, 1f)] public float bounciness = 0.1f;
    [Range(0f, 1f)] public float friction = 0.6f;

    [Header("굽힘")]
    [Tooltip("이 형태가 구부러질지. 끄면 rest 곡률을 유지하는 뻣뻣한 스퀴시(스트레스볼). 버터/베이컨처럼 드레이핑이 필요하면 켠다.")]
    public bool bendEnabled = true;
    [Tooltip("스파인 원래 곡률로 되돌리는 세기. 버터=완만, 바나나=초승달 유지.")]
    public float bendStiffness = 60f;
    public float bendDamping = 8f;
    [Tooltip("이음새 하나가 rest 각에서 벗어날 수 있는 최대 각(도)")]
    public float maxBendAnglePerJoint = 18f;

    [Header("스퀴시 메쉬 해상도")]
    [Tooltip("스파인 방향 정점 수 (표면 매끈함)")]
    [Range(2, 64)]
    public int meshCols = 20;
    [Tooltip("폭 방향 정점 수 (스퀴시 볼륨)")]
    [Range(2, 16)]
    public int meshRows = 5;
}
