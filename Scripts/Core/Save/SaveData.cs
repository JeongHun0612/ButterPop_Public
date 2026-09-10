using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public const int CurrentVersion = 1;   // 정식 출시 빌드의 세이브 구조. 출시 후 구조를 바꾸면 올린다

    public int version = CurrentVersion;
    public StageSaveData stage = new();
    public SkinSaveData skin = new();
    public PurchaseSaveData purchase = new();
    public SettingsSaveData settings = new();
}

[Serializable]
public class StageSaveData
{
    public int highestClearedIndex = -1;
}

[Serializable]
public class SkinSaveData
{
    public List<EquippedEntry> equipped = new();
}

[Serializable]
public class EquippedEntry
{
    public SkinCategory category;
    public string skinId;
}

[Serializable]
public class PurchaseSaveData
{
    public bool bannerRemoveAds;   // 배너 광고 제거
    public bool allRemoveAds;      // 전체 광고 제거(전면 포함) + 올스킨 언락
}

[Serializable]
public class SettingsSaveData
{
    public float bgmVolume = 1f;
    public float sfxVolume = 1f;
    public bool vibration = true;
    public bool muted;
}
