using System.Collections.Generic;

// 로드한 세이브를 현재 코드가 기대하는 상태로 맞춘다.
public static class SaveMigration
{
    public static void Apply(SaveData data)
    {
        if (data == null) return;

        EnsureSubObjects(data);

        // 정식 출시 후 세이브 구조를 바꾸면 여기에 버전별 변환을 순서대로 넣는다.
        //   if (data.version < 2) MigrateTo2(data);
        // 출시 전에는 구조가 바뀌어도 세이브를 지우면 되므로 변환 단계를 두지 않는다.

        // 앞선 버전의 세이브(신버전 앱에서 저장된 것)는 내려 찍지 않는다.
        if (data.version < SaveData.CurrentVersion) data.version = SaveData.CurrentVersion;
    }

    // JsonUtility는 JSON에 없는 하위 객체를 null로 남긴다. 잘린 파일·구버전 파일 방어를 겸한다.
    private static void EnsureSubObjects(SaveData data)
    {
        data.stage ??= new StageSaveData();
        data.skin ??= new SkinSaveData();
        data.skin.equipped ??= new List<EquippedEntry>();
        data.purchase ??= new PurchaseSaveData();
        data.settings ??= new SettingsSaveData();
    }
}
