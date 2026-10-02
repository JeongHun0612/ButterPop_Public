// 광고 1회 노출의 수익 정보. SDK 타입을 광고 모듈 밖으로 내보내지 않기 위한 값 객체.
public readonly struct AdImpression
{
    public readonly string Platform;   // 미디에이션 (ironSource)
    public readonly string Network;    // 광고를 채운 네트워크 (UnityAds 등)
    public readonly string Format;     // banner / interstitial / rewarded_video
    public readonly string UnitName;
    public readonly double Revenue;    // USD

    public AdImpression(string platform, string network, string format, string unitName, double revenue)
    {
        Platform = platform;
        Network = network;
        Format = format;
        UnitName = unitName;
        Revenue = revenue;
    }
}
