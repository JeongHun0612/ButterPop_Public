using System;
using System.Collections.Generic;
using UnityEngine;

// SfxType ↔ AudioClip 매핑 테이블. SoundManager가 인스펙터로 직접 참조한다.
// BGM이 필요해지면 아래에 별도 섹션(BgmType enum + 매핑)을 추가해 확장한다.
[CreateAssetMenu(fileName = "SoundDatabase", menuName = "BananaGallery/Sound/Sound Database")]
public class SoundDatabaseSO : ScriptableObject
{
    [Serializable]
    public class SfxEntry
    {
        public SfxType type;
        public AudioClip clip;
    }

    [Header("효과음")]
    [SerializeField] private SfxEntry[] sfxEntries;

    private Dictionary<SfxType, AudioClip> _sfxMap;

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
