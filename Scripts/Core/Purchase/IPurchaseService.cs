using System;
using Cysharp.Threading.Tasks;

public interface IPurchaseService
{
    bool RemoveAds { get; }
    UniTask InitializeAsync();
    UniTask<bool> PurchaseAsync(string productId);
    UniTask RestoreAsync();
    event Action OnPurchasesUpdated;
}
