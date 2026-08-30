using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 그리드 셀 1칸. 데이터/상태를 주입받아 표시만 담당한다.
/// 페이지 프리팹에 미리 배치되며, SkinPage가 Bind로 채운다.
/// </summary>
public class SkinCell : MonoBehaviour
{
    [Header("표시")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("상태")]
    [SerializeField] private GameObject lockOverlay;    // 미소유(잠금)
    [SerializeField] private GameObject selectedFrame;  // 선택 = 장착 (즉시 장착 모델)

    [Header("클릭")]
    [SerializeField] private Button button;

    private SkinDataSO _data;
    private Action<SkinDataSO> _onClick;
    private int _bindVersion;

    public SkinDataSO Data => _data;

    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(() => _onClick?.Invoke(_data));
    }

    public void Bind(SkinDataSO data, bool owned, bool selected, Action<SkinDataSO> onClick)
    {
        _data = data;
        _onClick = onClick;

        if (icon != null) icon.sprite = data.icon;
        RefreshNameAsync(data, ++_bindVersion).Forget();

        // 에디터에서는 개발 편의상 보유 여부와 무관하게 잠금을 해제하고, 빌드에서는 실제 보유 여부를 반영한다.
        if (lockOverlay != null)
        {
#if UNITY_EDITOR
            lockOverlay.SetActive(false);
#else
            lockOverlay.SetActive(!owned);
#endif
        }
        if (selectedFrame != null) selectedFrame.SetActive(selected);
    }

    public void SetSelected(bool selected)
    {
        if (selectedFrame != null) selectedFrame.SetActive(selected);
    }

    private async UniTaskVoid RefreshNameAsync(SkinDataSO data, int bindVersion)
    {
        if (nameText == null) return;

        string localizedName = data.LocalizedName == null || data.LocalizedName.IsEmpty
            ? data.id
            : await data.LocalizedName.GetLocalizedStringAsync();

        if (bindVersion == _bindVersion && _data == data)
            nameText.text = localizedName;
    }
}
