using UnityEngine;
using UnityEngine.Localization;

// 스킨의 공통 메타데이터(식별 + UI 아이콘)만 담는 추상 베이스.
// - 소유/장착 저장(SkinService)과 커스텀 UI는 이 베이스 타입만 알면 된다(id/icon).
// - 실제 교체 payload(스퀴시 스프라이트·타격음, 팔 스프라이트 등)는 카테고리별 파생 SO가 담당한다.
//   예: SquishSkinSO : SkinDataSO, ArmSkinSO : SkinDataSO
public abstract class SkinDataSO : ScriptableObject
{
    [Header("식별")]
    public string id;
    [Tooltip("무료 제공 스킨. 해금 없이 보유로 취급한다.")]
    public bool isFree;

    [Header("표시 정보")]
    [SerializeField] private LocalizedString localizedName = new();
    public Sprite icon;

    public LocalizedString LocalizedName => localizedName;
}
