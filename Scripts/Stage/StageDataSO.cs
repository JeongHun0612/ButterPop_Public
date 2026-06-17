using System;
using UnityEngine;

[CreateAssetMenu(fileName = "StageData", menuName = "BananaGallery/Stage Data")]
public class StageDataSO : ScriptableObject
{
    [Header("스테이지 정보")]
    public int stageId;
    public string stageName;

    [Header("바나나 스폰")]
    public Vector2 bananaSpawnPosition;

    [Header("카메라")]
    public Vector2 cameraCenter;
    public float cameraOrthographicSize = 5f;

    [Header("스테이지 오브젝트")]
    public StageObjectSpawnData[] stageObjects;

    [Header("판정")]
    public float fallYThreshold = -8f;
}

[Serializable]
public class StageObjectSpawnData
{
    public GameObject prefab;
    public Sprite sprite;
    public Vector2 position;
    public float rotation;
    public Vector2 scale = Vector2.one;
}
