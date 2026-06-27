using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class UIPopup : UIPanel
{
    public void OnClickBack()
    {
        UIManager.Instance.HidePopup().Forget();
    }
}
