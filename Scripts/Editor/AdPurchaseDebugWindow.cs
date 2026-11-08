using UnityEditor;
using UnityEngine;

// 광고 정책·구매 권한 개발용 확인 창. 플레이 중에만 동작한다.
// 디버그 해금 빌드는 권한이 항상 켜져 있어 광고 정책을 볼 수 없으므로 릴리즈 정책 프로필에서 사용한다.
public class AdPurchaseDebugWindow : EditorWindow
{
    private const float ButtonHeight = 24f;
    private const float LabelWidth = 150f;

    // 라이트·다크 테마 모두에서 읽히는 중간 톤
    private static readonly Color OnColor = new(0.36f, 0.75f, 0.42f);
    private static readonly Color OffColor = new(0.88f, 0.38f, 0.35f);
    private static readonly Color WarnColor = new(0.88f, 0.63f, 0.19f);
    private static readonly Color IdleColor = new(0.6f, 0.6f, 0.6f);

    private Vector2 _scroll;
    private GUIStyle _titleStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _badgeStyle;
    private GUIStyle _valueStyle;

    [MenuItem("ButterPop/광고·결제 디버그 창")]
    private static void Open()
    {
        var window = GetWindow<AdPurchaseDebugWindow>("광고·결제 디버그");
        window.minSize = new Vector2(340f, 480f);
        window.Show();
    }

    // 상태 값이 실시간으로 바뀌므로 플레이 중에는 계속 다시 그린다.
    private void OnInspectorUpdate()
    {
        if (Application.isPlaying) Repaint();
    }

    private void OnGUI()
    {
        InitStyles();
        EditorGUIUtility.labelWidth = LabelWidth;

        DrawHeader();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 중에만 사용할 수 있습니다.", MessageType.Info);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawPurchaseSection();
        DrawAdSection();
        DrawActionSection();

        EditorGUILayout.EndScrollView();
    }

    private void InitStyles()
    {
        if (_titleStyle != null) return;

        _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        _sectionStyle = new GUIStyle(EditorStyles.boldLabel) { margin = new RectOffset(2, 2, 2, 6) };
        _badgeStyle = new GUIStyle(EditorStyles.miniButton)
        {
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            fixedHeight = 18f
        };
        _valueStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("광고·결제 디버그", _titleStyle, GUILayout.Height(20f));
        GUILayout.FlexibleSpace();
        DrawBadge(BuildFlags.IsRelease ? "릴리즈 정책" : "디버그 해금", BuildFlags.IsRelease ? OnColor : WarnColor, 90f);
        EditorGUILayout.EndHorizontal();

        if (BuildFlags.DebugUnlock)
            EditorGUILayout.HelpBox("디버그 해금 정책이라 광고 제거 권한이 항상 켜져 있습니다. " +
                "광고 정책을 보려면 앱인토스·Android 릴리즈 정책 빌드 프로필을 활성화하세요.", MessageType.Warning);

        EditorGUILayout.Space(4f);
    }

    private void DrawPurchaseSection()
    {
        BeginSection("구매 권한");

        if (!Services.TryGet<ISaveService>(out var save) || !Services.TryGet<IPurchaseService>(out var purchase))
        {
            EditorGUILayout.HelpBox("구매·세이브 서비스가 아직 등록되지 않았습니다.", MessageType.Info);
            EndSection();
            return;
        }

        var data = save.Data.purchase;
        EditorGUILayout.BeginHorizontal();
        DrawEntitlementToggle(purchase, "배너 광고 제거", ProductIds.RemoveBannerAds, data.bannerRemoveAds);
        DrawEntitlementToggle(purchase, "전체 광고 제거", ProductIds.RemoveAllAds, data.allRemoveAds);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        bool hasPrice = purchase.TryGetPrice(ProductIds.RemoveAllAds, out string price);
        DrawStatusRow("전체 광고 제거 가격", hasPrice ? price : "조회 실패", hasPrice ? OnColor : OffColor);

        EndSection();
    }

    private static void DrawEntitlementToggle(IPurchaseService purchase, string label, string productId, bool owned)
    {
        var prevColor = GUI.backgroundColor;
        GUI.backgroundColor = owned ? OnColor : Color.white;
        bool next = GUILayout.Toggle(owned, $"{label}\n{(owned ? "보유" : "미보유")}", "Button", GUILayout.Height(36f));
        GUI.backgroundColor = prevColor;

        if (next == owned) return;

        if (purchase is not PurchaseService service)
        {
            Debug.LogWarning("[AdPurchaseDebugWindow] PurchaseService가 아니라 권한을 바꿀 수 없습니다");
            return;
        }

        service.DebugSetEntitlement(productId, next);
    }

    private void DrawAdSection()
    {
        BeginSection("광고 정책");

        if (!AdCoordinator.TryGetInstance(out var ad) || !ad.DebugInitialized)
        {
            EditorGUILayout.HelpBox("AdCoordinator가 아직 초기화되지 않았습니다.", MessageType.Info);
            EndSection();
            return;
        }

        DrawProgressRow("세션 유예", ad.DebugSessionPlaySeconds, ad.DebugMinSessionPlaySeconds,
            $"{ad.DebugSessionPlaySeconds:0} / {ad.DebugMinSessionPlaySeconds:0}초");
        DrawStatusRow("전면 게이트", ad.DebugCanShowInterstitial ? "통과" : "막힘",
            ad.DebugCanShowInterstitial ? OnColor : OffColor);
        DrawStatusRow("이번 스테이지 리워드", ad.RewardWatchedThisStage ? "시청함" : "안 봄",
            ad.RewardWatchedThisStage ? WarnColor : IdleColor);

        EditorGUILayout.Space(4f);
        DrawProgressRow("전면 이후 실패", ad.DebugFailsSinceInterstitial, ad.DebugInterstitialFailThreshold,
            $"{ad.DebugFailsSinceInterstitial} / {ad.DebugInterstitialFailThreshold}");
        DrawProgressRow("전면 이후 클리어", ad.DebugClearsSinceInterstitial, ad.DebugInterstitialClearThreshold,
            $"{ad.DebugClearsSinceInterstitial} / {ad.DebugInterstitialClearThreshold}");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("광고 제거 버튼", _sectionStyle);
        DrawStatusRow("노출", ad.IsRemoveAdsPromptActive ? "노출 중" : "숨김",
            ad.IsRemoveAdsPromptActive ? OnColor : IdleColor);
        DrawProgressRow("남은 실패 횟수", ad.DebugRemoveAdsPromptFailsLeft, ad.DebugRemoveAdsPromptFailCount,
            $"{ad.DebugRemoveAdsPromptFailsLeft} / {ad.DebugRemoveAdsPromptFailCount}");
        DrawStatusRow("다음 스테이지 예약", ad.DebugRemoveAdsPromptPending ? "예약됨" : "없음",
            ad.DebugRemoveAdsPromptPending ? WarnColor : IdleColor);

        EndSection();
    }

    private void DrawActionSection()
    {
        BeginSection("동작");

        bool hasAd = AdCoordinator.TryGetInstance(out var ad) && ad.DebugInitialized;
        bool hasStage = StageManager.TryGetInstance(out var stage);

        EditorGUILayout.LabelField("광고", EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(!hasAd))
        {
            EditorGUILayout.BeginHorizontal();
            if (ActionButton("세션 유예 건너뛰기")) ad.DebugSkipSessionGrace();
            if (ActionButton("실패 전면 광고 시도")) ad.DebugTryShowInterstitial();
            EditorGUILayout.EndHorizontal();

            if (ActionButton("광고 제거 버튼 바로 띄우기")) ad.DebugStartRemoveAdsPrompt();
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("스테이지", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!hasStage))
        {
            if (ActionButton("실패 1회")) stage.DebugCountFail();
        }
        using (new EditorGUI.DisabledScope(!hasAd || !hasStage))
        {
            if (ActionButton("리워드 건너뛰기", "광고 재생 없이 건너뛰기 보상을 받은 것으로 처리한다"))
            {
                ad.MarkRewardWatched();
                stage.SkipCurrentStage();
            }
        }
        EditorGUILayout.EndHorizontal();

        EndSection();
    }

    private void BeginSection(string title)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(title, _sectionStyle);
    }

    private static void EndSection()
    {
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(6f);
    }

    private void DrawStatusRow(string label, string value, Color color)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(LabelWidth));
        GUILayout.FlexibleSpace();
        DrawBadge(value, color, 90f);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawProgressRow(string label, float value, float max, string text)
    {
        var rect = EditorGUILayout.GetControlRect(GUILayout.Height(18f));
        var barRect = EditorGUI.PrefixLabel(rect, new GUIContent(label));
        EditorGUI.ProgressBar(barRect, max > 0f ? Mathf.Clamp01(value / max) : 0f, text);
    }

    private void DrawBadge(string text, Color color, float width)
    {
        var prevColor = GUI.backgroundColor;
        GUI.backgroundColor = color;
        GUILayout.Label(text, _badgeStyle, GUILayout.Width(width));
        GUI.backgroundColor = prevColor;
    }

    private static bool ActionButton(string text, string tooltip = null) =>
        GUILayout.Button(new GUIContent(text, tooltip), GUILayout.Height(ButtonHeight));
}
