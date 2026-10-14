using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StageList", menuName = "ButterPop/Stage/Stage List")]
public class StageListSO : ScriptableObject
{
    public List<StageDataSO> stages = new();

    public int Count => stages.Count;
    public StageDataSO Get(int index) => stages[index];
    public bool IsValidIndex(int index) => index >= 0 && index < stages.Count;
}
