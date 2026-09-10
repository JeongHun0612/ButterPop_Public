using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoSingleton<SoundManager>
{
    [Header("사운드 데이터")]
    [SerializeField] private SoundDatabaseSO database;

    [Header("재생")]
    [Tooltip("효과음 재생용 공용 AudioSource. PlayOneShot으로 겹쳐 재생한다")]
    [SerializeField] private AudioSource sfxSource;

    private bool _muted;

    public bool IsMuted => _muted;

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

    public void Initialize()
    {
        bool muted = Services.TryGet<ISaveService>(out var save) && save.Data.settings.muted;
        ApplyMute(muted);
    }

    public void SetMuted(bool muted)
    {
        if (_muted == muted) return;

        ApplyMute(muted);

        if (Services.TryGet<ISaveService>(out var save))
        {
            save.Data.settings.muted = muted;
            save.Save();
        }
    }

    public void ToggleMute() => SetMuted(!_muted);

    // 공용 효과음 재생. SoundDatabaseSO에서 클립을 조회해 재생한다.
    public void PlaySfx(SfxType type, float volumeScale = 1f)
    {
        if (_muted) return;
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
        if (_muted) return;
        if (clip == null) return;

        // 타입 조회/직접 재생 모두 여기서 한 번만 클립별 가중치를 적용한다.
        float multiplier = database != null ? database.GetVolumeMultiplier(clip) : 1f;
        sfxSource.PlayOneShot(clip, Mathf.Max(0f, volumeScale) * multiplier);
    }

    private void ApplyMute(bool muted)
    {
        _muted = muted;
        AudioListener.volume = muted ? 0f : 1f;
    }
}
