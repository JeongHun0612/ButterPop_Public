using System.Collections.Generic;
using System.Threading;

// 현재 스테이지의 살아있는 액터 참조 묶음
public class StageContext
{
    public StageDataSO Data;
    public Squish Squish;
    public ArmController ArmController;
    public CancellationToken StageCancellationToken;
    public readonly List<IStageClearEffect> ClearEffects = new();
}
