using UnityEngine;

// 스퀴시 오브젝트의 "스킨" = 물리를 제외한 교체 단위(비주얼 + 사운드).
// - 물리/형태는 SquishBody(제어점·물성 인라인)가, 비주얼·사운드는 이 SO가 담당한다.
// - 같은 직사각형 물리를 공유하면서 스프라이트 + 타격음만 바꿔 다양한 오브젝트(버터·키캡키링·슬라임…)를 만든다.
// - 오브젝트 하나당 이 SO 하나. SquishMesh(스프라이트) · SquishSound(타격음)가 참조한다.
[CreateAssetMenu(fileName = "SquishSkin", menuName = "BananaGallery/Squish/Squish Skin")]
public class SquishSkinSO : ScriptableObject
{
    [Header("비주얼")]
    public Sprite sprite;

    [Header("타격 사운드 (ASMR)")]
    [Tooltip("충돌 시 SoundManager로 재생할 클립.")]
    public AudioClip hitClip;
}
