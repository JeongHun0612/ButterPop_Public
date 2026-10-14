using System;
using UnityEngine;

// 트리거 영역 진입·이탈을 이벤트로 알리는 감지 존. 물리 반응 없이 통과하는 영역에 쓴다.
public class SquishTriggerSensor : SquishSensor
{
    public event Action OnSquishEntered;   // 첫 뼈가 들어왔을 때 1회
    public event Action OnSquishExited;    // 마지막 뼈가 나갔을 때 1회

    // 감지 전용이므로 트리거로 강제한다.
    private void Awake() => GetComponent<Collider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (TryAddSquish(other))
            OnSquishEntered?.Invoke();
    }

    private void OnTriggerExit2D(Collider2D other) => RemoveSquish(other);

    protected override void OnSquishEnded() => OnSquishExited?.Invoke();
}
