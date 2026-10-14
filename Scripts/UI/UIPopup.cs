using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public abstract class UIPopup : UIPanel
{
    [Header("Dimmed Background")]
    [SerializeField] private Button dimmed;
    [SerializeField] private bool closeOnDimmedClick = true;

    [Header("컨텐츠")]
    [Tooltip("Dimmed를 제외한 팝업 내용 (보통 SafeAreaContent)")]
    [SerializeField] private CanvasGroup content;

    public override void Initialize()
    {
        base.Initialize();

        if (dimmed != null && closeOnDimmedClick)
            dimmed.onClick.AddListener(OnClickBack);

        if (content == null)
            Debug.LogWarning($"[UIPopup] content가 비어 있어 연출 중 클릭이 잠기지 않습니다: {name}");
    }

    // 컨텐츠의 raycast를 끄면 클릭이 Dimmed의 Image에서 멈춘다. 그 사이 Dimmed로 닫히지 않게 함께 잠근다.
    protected override void SetInputGuard(bool guarded)
    {
        if (content != null) content.blocksRaycasts = !guarded;
        if (dimmed != null) dimmed.interactable = !guarded;
    }

    public virtual void OnClickBack()
    {
        var uiManager = UIManager.Instance;
        Debug.Assert(uiManager != null, "[UIPopup] UIManager가 없습니다");
        uiManager.HidePopup(this).Forget();
    }
}
