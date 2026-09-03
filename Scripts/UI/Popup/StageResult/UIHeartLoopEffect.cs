using Coffee.UIExtensions;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIParticle))]
public sealed class UIHeartLoopEffect : MonoBehaviour
{
    private UIParticle _particle;

    private void Awake()
    {
        _particle = GetComponent<UIParticle>();
    }

    private void OnEnable()
    {
        // 부모 프레임이 먼저 켜져도 명시적인 Play 호출 전에는 방출하지 않는다.
        Stop();
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        if (!isActiveAndEnabled) return;
        if (_particle == null) _particle = GetComponent<UIParticle>();
        _particle.Play();
    }

    public void Stop()
    {
        // 비활성 상태에서 UIStageResult가 먼저 호출할 수 있다.
        if (_particle == null) _particle = GetComponent<UIParticle>();
        // 재표시할 때 이전 프레임의 하트가 남지 않도록 즉시 비운다.
        _particle.Stop();
        _particle.Clear();
    }
}
