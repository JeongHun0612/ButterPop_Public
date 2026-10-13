// 빌드 종류에 따른 광고·결제 정책 판정을 한 곳에 모은다.
// 실제 광고와 인앱 결제는 모바일 또는 Apps in Toss 채널에서 동작하고,
// 그 외 플랫폼은 테스트용이라 광고를 끄고 광고 제거 상품을 보유한 것으로 취급한다.
public static class BuildFlags
{
    // 릴리즈 정책으로 동작하는 빌드인지(= 모바일 또는 Apps in Toss + 디버그 해금·시연 디파인 없음).
    // 에디터는 활성 빌드 프로필의 디파인을 따라가므로, 릴리즈 프로필을 켜면 에디터도 릴리즈로 동작한다.
#if (UNITY_ANDROID || UNITY_IOS || (APPS_IN_TOSS && UNITY_WEBGL)) && !BUTTERPOP_DEBUG_UNLOCK && !BUTTERPOP_DEMO
    public static readonly bool IsRelease = true;
#else
    public static readonly bool IsRelease = false;
#endif

    // 시연용 빌드인지. 디버그 해금 정책에 더해 랭킹을 끈다.
#if BUTTERPOP_DEMO
    public static readonly bool IsDemo = true;
#else
    public static readonly bool IsDemo = false;
#endif

    // 결제 없이 광고 제거·전체 스킨 권한을 부여할지(구매 세이브는 건드리지 않고 실행 중 권한에만 적용).
    public static readonly bool DebugUnlock = !IsRelease;

    // 실제 스토어 결제를 붙일지. 에디터는 스토어에 붙을 수 없어 릴리즈여도 스텁을 쓴다.
#if UNITY_EDITOR
    public static readonly bool UseLiveIap = false;
#else
    public static readonly bool UseLiveIap = IsRelease;
#endif
}
