using System;
using System.Collections.Generic;
using UnityEngine;

// SfxType ↔ AudioClip 매핑 테이블. SoundManager가 인스펙터로 직접 참조한다.
// BGM이 필요해지면 아래에 별도 섹션(BgmType enum + 매핑)을 추가해 확장한다.
[CreateAssetMenu(fileName = "SoundDatabase", menuName = "ButterPop/Sound/Sound Database")]
public class SoundDatabaseSO : ScriptableObject
{
    [Serializable]
    public class SfxEntry
    {
        public SfxType type;
        public AudioClip clip;
    }

    [Serializable]
    public class ClipVolumeEntry
    {
        public AudioClip clip;

        [Min(0f)]
        public float volumeMultiplier = 1f;
    }

    [Header("효과음")]
    [SerializeField] private SfxEntry[] sfxEntries;

    [Header("클립별 볼륨 가중치\n0 = 음소거 · 1 = 원래 볼륨 · 2 = 2배")]
    [Tooltip("효과음 타입과 직접 재생하는 클립 모두에 적용한다. 미등록 클립은 1배, 중복 클립은 첫 항목을 사용한다.")]
    [SerializeField] private ClipVolumeEntry[] clipVolumes;

    private Dictionary<SfxType, AudioClip> _sfxMap;
    private Dictionary<AudioClip, ClipVolumeEntry> _clipVolumeMap;

    private void OnEnable() => ClearCache();

    // 플레이 중 인스펙터 수정도 다음 재생부터 반영한다.
    private void OnValidate() => ClearCache();

    private void ClearCache()
    {
        _sfxMap = null;
        _clipVolumeMap = null;
    }

    public float GetVolumeMultiplier(AudioClip clip)
    {
        if (clip == null) return 1f;

        EnsureClipVolumesBuilt();
        return _clipVolumeMap.TryGetValue(clip, out var entry)
            ? Mathf.Max(0f, entry.volumeMultiplier)
            : 1f;
    }

    private void EnsureClipVolumesBuilt()
    {
        if (_clipVolumeMap != null) return;

        _clipVolumeMap = new Dictionary<AudioClip, ClipVolumeEntry>();
        if (clipVolumes == null) return;

        foreach (var entry in clipVolumes)
        {
            if (entry == null || entry.clip == null) continue;

            if (_clipVolumeMap.ContainsKey(entry.clip))
            {
                Debug.LogWarning($"[SoundDatabaseSO] 볼륨 가중치 클립 중복: {entry.clip.name}. 첫 항목만 사용한다");
                continue;
            }

            _clipVolumeMap[entry.clip] = entry;
        }
    }

    // SfxType에 매핑된 클립을 반환한다. 미등록이면 null.
    public AudioClip GetSfx(SfxType type)
    {
        EnsureBuilt();
        return _sfxMap.TryGetValue(type, out var clip) ? clip : null;
    }

    // 첫 조회 시 배열을 딕셔너리로 캐싱한다. None/중복 키는 건너뛴다.
    private void EnsureBuilt()
    {
        if (_sfxMap != null) return;

        _sfxMap = new Dictionary<SfxType, AudioClip>();
        if (sfxEntries == null) return;

        foreach (var entry in sfxEntries)
        {
            if (entry == null || entry.type == SfxType.None || entry.clip == null)
                continue;

            if (_sfxMap.ContainsKey(entry.type))
            {
                Debug.LogWarning($"[SoundDatabaseSO] SfxType 중복: {entry.type}. 첫 항목만 사용한다");
                continue;
            }

            _sfxMap[entry.type] = entry.clip;
        }
    }
}
