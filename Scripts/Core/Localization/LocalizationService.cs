using Cysharp.Threading.Tasks;
using UnityEngine.Localization.Settings;

public class LocalizationService : ILocalizationService
{
    public async UniTask InitializeAsync()
    {
        await LocalizationSettings.InitializationOperation;
    }

}
