using UnityEngine;

// 스퀴시 오브젝트의 "스킨" = 물리를 제외한 교체 단위(비주얼 + 사운드).
// - 물리/형태는 SquishShapeSO(공유)가, 비주얼·사운드는 이 SO가 담당한다.
// - 같은 직사각형 물리를 공유하면서 스프라이트 + 타격음만 바꿔 다양한 오브젝트(버터·키캡키링·슬라임…)를 만든다.
// - 오브젝트 하나당 이 SO 하나. SquishMesh(스프라이트/opaqueUVRect) · SquishSound(타격음)가 참조한다.
[CreateAssetMenu(fileName = "SquishSkin", menuName = "BananaGallery/Squish/Squish Skin")]
public class SquishSkinSO : ScriptableObject
{
    [Header("비주얼")]
    public Sprite sprite;
    [Tooltip("스프라이트 rect 내 실제 불투명(아트) 영역, 0~1. 인스펙터의 '불투명영역 자동계산' 버튼으로 채운다. 여백 큰 스프라이트도 그림 영역에만 정확히 정합.")]
    [HideInInspector] public Rect opaqueUVRect = new Rect(0f, 0f, 1f, 1f);

    [Header("타격 사운드 (ASMR)")]
    [Tooltip("충돌 시 SoundManager로 재생할 클립.")]
    public AudioClip hitClip;
}
