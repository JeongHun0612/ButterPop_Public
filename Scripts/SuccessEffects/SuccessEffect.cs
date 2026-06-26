using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

public abstract class SuccessEffect : MonoBehaviour
{
    public abstract UniTask PlayEffect(CancellationToken ct = default);
}
