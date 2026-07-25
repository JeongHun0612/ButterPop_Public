using System;
using UnityEngine;

[CreateAssetMenu(fileName = "StageData", menuName = "BananaGallery/Stage/Stage Data")]
public class StageDataSO : ScriptableObject
{
    // 오브젝트·카메라 배치 기준 화면 비율(가로/세로). 19.5:9 (1170x2532)
    public const float ReferenceAspect = 9f / 19.5f;

    // 바나나를 가리는 오브젝트에 적용할 Sorting Order
    public const int CoverBananaSortingOrder = 100;

    // 스테이지 기본 배경색
    public static readonly Color DefaultBackgroundColor =
        ColorUtility.TryParseHtmlString("#3C3C3C", out var color) ? color : Color.gray;

    /// <summary>
    /// 기준 비율에서 잡은 orthographicSize를 실제 기기 비율에 맞춰 보정한다.
    /// 세로가 더 긴 기기는 가로가 잘리지 않게 size를 키우고(위아래 여백↑), 더 넓은 기기는 그대로 둔다(좌우 여백↑).
    /// 결과적으로 의도한 사각형은 어떤 기기에서도 전부 보인다.
    /// </summary>
    public static float ResolveOrthographicSize(float authoredSize, float currentAspect)
    {
        if (currentAspect <= 0f) return authoredSize;

        float widthFitSize = authoredSize * ReferenceAspect / currentAspect;
        return Mathf.Max(authoredSize, widthFitSize);
    }

    [Header("스테이지 정보")]
    public string stageName;
    [TextArea] public string stageDescription;

    [Header("바나나 낙하 시작점")]
    public Vector2 bananaSpawnPosition;

    [Header("원숭이 팔")]
    public Vector2 monkeyArmPosition;

    [Header("카메라")]
    public Vector2 cameraCenter;
    public float cameraOrthographicSize = 6f;

    [Header("배경")]
    public Color backgroundColor = DefaultBackgroundColor;

    [Header("스테이지 오브젝트")]
    public StageObjectSpawnData[] stageObjects;
}

[Serializable]
public class StageObjectSpawnData
{
    public GameObject prefab;
    public Sprite sprite;
    public Vector2 position;
    public float rotation;
    public Vector2 scale = Vector2.one;
    public bool flipX;

    // true면 Sorting Order를 높여(StageDataSO.CoverBananaSortingOrder) 바나나를 가린다. 돔 내부 등에 사용.
    public bool coverBanana;
}