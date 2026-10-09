using System;
using UnityEngine;

// 이 RectTransform의 anchor를 기기 안전 영역(노치·홈 인디케이터를 뺀 영역)에 맞춘다.
// 전체 화면 배경이 있는 패널은 배경의 자식에 붙여 배경만 노치 뒤까지 늘어나게 한다.
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    [Header("맞출 축")]
    [Tooltip("끄면 가로는 안전 영역을 무시하고 화면 끝까지 쓴다")]
    [SerializeField] private bool conformX = true;
    [Tooltip("끄면 세로는 안전 영역을 무시하고 화면 끝까지 쓴다")]
    [SerializeField] private bool conformY = true;

    private RectTransform _panel;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;
    private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

    // Screen.safeArea 대신 쓸 안전 영역(스크린 px, 좌하단 원점). null이면 Screen.safeArea를 쓴다.
    public static Func<Rect> SafeAreaProvider { get; set; }

    private void Awake()
    {
        _panel = GetComponent<RectTransform>();
        Refresh();
    }

    private void Update() => Refresh();

    private void Refresh()
    {
        Rect safeArea = SafeAreaProvider != null ? SafeAreaProvider() : Screen.safeArea;

        // 자동 회전을 끄고 방향을 강제로 바꾸면 safeArea 값은 그대로인데 화면 크기·방향만 바뀌는 경우가 있어 함께 비교한다.
        if (safeArea == _lastSafeArea
            && Screen.width == _lastScreenSize.x
            && Screen.height == _lastScreenSize.y
            && Screen.orientation == _lastOrientation)
            return;

        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        _lastOrientation = Screen.orientation;
        Apply(safeArea);
    }

    // 안전 영역(스크린 px)을 정규화해 anchor로 넣는다.
    private void Apply(Rect area)
    {
        _lastSafeArea = area;

        if (!conformX)
        {
            area.x = 0;
            area.width = Screen.width;
        }

        if (!conformY)
        {
            area.y = 0;
            area.height = Screen.height;
        }

        // 일부 삼성 기기는 시작 직후 화면 크기가 0으로 잡히는 프레임이 있다.
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Vector2 anchorMin = area.position;
        Vector2 anchorMax = area.position + area.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        // 같은 기기들에서 첫 호출의 anchor가 NaN으로 나오는 경우를 거른다(NaN은 비교가 모두 false).
        if (anchorMin.x >= 0 && anchorMin.y >= 0 && anchorMax.x >= 0 && anchorMax.y >= 0)
        {
            _panel.anchorMin = anchorMin;
            _panel.anchorMax = anchorMax;
        }
    }
}
