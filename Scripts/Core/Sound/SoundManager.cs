using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoSingleton<SoundManager>
{
    // 세이브 데이터의 음소거 설정을 기기 저장소에 둔 사본. 세이브가 준비되기 전에 나는 소리에 쓴다.
    private const string MutedCacheKey = "sound.muted";

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

        // 로딩 인트로 소리는 세이브 로드보다 먼저 나므로 사본으로 음소거를 먼저 적용한다.
        ApplyMute(PlayerPrefs.GetInt(MutedCacheKey, 0) == 1);
    }

    // 세이브 데이터가 기준이다. 로드에 실패한 세션은 기본값이 들어 있으므로 사본을 유지한다.
    public void Initialize()
    {
        if (!Services.TryGet<ISaveService>(out var save) || save.IsReadOnly) return;

        bool muted = save.Data.settings.muted;
        ApplyMute(muted);
        CacheMuted(muted);
    }

    public void SetMuted(bool muted)
    {
        if (_muted == muted) return;

        ApplyMute(muted);
        CacheMuted(muted);

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

    // 인스턴스가 없으면 재생하지 않는다. 스테이지 오브젝트처럼 SoundManager 없이도 동작해야 하는 곳에서 쓴다.
    public static void TryPlaySfx(AudioClip clip)
    {
        if (TryGetInstance(out var sound)) sound.PlaySfx(clip);
    }

    private void ApplyMute(bool muted)
    {
        _muted = muted;
        AudioListener.volume = muted ? 0f : 1f;
    }

    // 앱이 강제 종료돼도 남도록 값이 바뀔 때 바로 기록한다.
    private static void CacheMuted(bool muted)
    {
        int value = muted ? 1 : 0;
        if (PlayerPrefs.GetInt(MutedCacheKey, 0) == value) return;

        PlayerPrefs.SetInt(MutedCacheKey, value);
        PlayerPrefs.Save();
    }
}
