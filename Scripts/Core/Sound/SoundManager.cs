using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoSingleton<SoundManager>
{
    [Header("사운드 데이터")]
    [SerializeField] private SoundDatabaseSO database;

    [Header("재생")]
    [Tooltip("효과음 재생용 공용 AudioSource. PlayOneShot으로 겹쳐 재생한다")]
    [SerializeField] private AudioSource sfxSource;

    protected override void Awake()
    {
        base.Awake();

        // 인스펙터 미할당 시 컴포넌트에서 캐싱한다.
        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();

        // 2D 게임이므로 위치 기반 감쇠 없이 재생한다.
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
    }

    // 공용 효과음 재생. SoundDatabaseSO에서 클립을 조회해 재생한다.
    public void PlaySfx(SfxType type, float volumeScale = 1f)
    {
        if (type == SfxType.None) return;

        Debug.Assert(database != null, "[SoundManager] SoundDatabaseSO가 없습니다");
        var clip = database.GetSfx(type);
        if (clip == null)
        {
            Debug.LogWarning($"[SoundManager] 미등록 SfxType 재생 시도: {type}");
            return;
        }

        PlaySfx(clip, volumeScale);
    }

    // 오브젝트가 소유한 클립을 직접 재생한다.
    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;

        sfxSource.PlayOneShot(clip, volumeScale);
    }
}
