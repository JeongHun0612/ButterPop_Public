using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int version = 1;
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
    public List<string> ownedSkinIds = new();
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
    public bool removeAds;
    public List<string> purchasedProductIds = new();
}

[Serializable]
public class SettingsSaveData
{
    public float bgmVolume = 1f;
    public float sfxVolume = 1f;
    public bool vibration = true;
}
