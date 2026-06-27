using System;
using UnityEngine;

[CreateAssetMenu(fileName = "StageData", menuName = "BananaGallery/Stage/Stage Data")]
public class StageDataSO : ScriptableObject
{
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