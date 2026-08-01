using System;
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
    [SerializeField] private TextMeshProUGUI priceText;

    [Header("상태")]
    [SerializeField] private GameObject lockOverlay;    // 미소유(잠금)
    [SerializeField] private GameObject equippedMark;   // 장착중
    [SerializeField] private GameObject selectedFrame;  // 선택(장착 후보)

    [Header("클릭")]
    [SerializeField] private Button button;

    private SkinDataSO _data;
    private Action<SkinDataSO> _onClick;

    public SkinDataSO Data => _data;

    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(() => _onClick?.Invoke(_data));
    }

    public void Bind(SkinDataSO data, bool owned, bool equipped, bool selected, Action<SkinDataSO> onClick)
    {
        _data = data;
        _onClick = onClick;

        if (icon != null) icon.sprite = data.icon;
        if (nameText != null) nameText.text = data.displayName;
        if (priceText != null) priceText.text = $"{data.price}GB";

        // TODO: 테스트용 — 모든 스킨을 소유 상태로 표시. 실제 로직 복원 시 아래 줄을 원복할 것.
        // if (lockOverlay != null) lockOverlay.SetActive(!owned);
        if (lockOverlay != null) lockOverlay.SetActive(false);
        if (equippedMark != null) equippedMark.SetActive(equipped);
        if (selectedFrame != null) selectedFrame.SetActive(selected);
    }

    public void SetSelected(bool selected)
    {
        if (selectedFrame != null) selectedFrame.SetActive(selected);
    }
}
