using System;
using UnityEngine.UI;

public static class ButtonExtensions
{
    public static void OnClickWithSound(this Button button, Action callback)
    {
        button.onClick.AddListener(() =>
        {
            if (SoundManager.TryGetInstance(out var sound))
                sound.PlaySfx(SfxType.ButtonClick);

            callback?.Invoke();
        });
    }
}
