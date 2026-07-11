using System;
using UnityEngine;

[CreateAssetMenu(fileName = "StageData", menuName = "BananaGallery/Stage/Stage Data")]
public class StageDataSO : ScriptableObject
{
    /// <summary>
    /// 배치 기준 화면 비율(가로/세로). "가장 많이 쓰는 해상도"로 잡는다. 1170x2532 = 19.5:9.
    /// 작가는 이 비율의 Game View에서 카메라 줌과 오브젝트를 배치하며, 이 비율에서 의도한 구도(원숭이 절반)가 나온다.
    /// 이 기준을 중심으로 양쪽은 어쩔 수 없이 타협된다:
    /// - 이보다 넓은 기기: 원숭이가 의도보다 더 많이 노출된다(좌우가 더 보임).
    /// - 이보다 더 긴(좁은) 기기: orthoSize를 키워(줌아웃) 원숭이 배치라도 절반으로 사수한다.
    /// 따라서 다수 유저가 몰린 비율을 기준에 두어 타협 인구를 최소화한다. 흔한 범위(19.5:9~20:9)의 중앙값.
    /// </summary>
    public const float ReferenceAspect = 9f / 19.5f;

    /// <summary>
    /// 기준 비율에서 배치한 orthographicSize를 실제 기기 비율에 맞춰 보정한다.
    /// - 기준보다 좁은(세로가 긴) 기기: 가로가 잘리지 않도록 size를 키워 폭을 보존(위아래 여백 증가).
    /// - 기준보다 넓은 기기: 세로가 잘리지 않도록 authored 값을 유지(좌우 여백 증가).
    /// 결과적으로 작가가 의도한 사각형(가로·세로)은 어떤 기기에서도 항상 전부 보인다.
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
    public float cameraOrthographicSize = 5f;

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
}