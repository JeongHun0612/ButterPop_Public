public enum GameState
{
    None,
    StageReady,
    Ready,
    Falling,
    Result
}

public enum SfxType
{
    None,
    ButtonClick,
    SquishHit,
    SquishSettle,
    StageClear,
    StageFail
}

// 배너 광고가 화면 상/하 어느 쪽에 붙는지.
public enum BannerSide
{
    Top,
    Bottom
}

// 인앱 결제 시도의 결과.
public enum PurchaseState
{
    Success,
    Cancelled,
    Failed,
    Pending     // 지연 결제(편의점 등) 대기. 완료되면 스토어가 나중에 다시 알린다
}
