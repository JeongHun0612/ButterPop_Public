using UnityEngine;

// 세그먼트 각각의 충돌을 부모 ButterBody로 올려주는 릴레이. (SquishContactRelay와 동일 패턴)
[RequireComponent(typeof(Rigidbody2D))]
public class ButterSegmentRelay : MonoBehaviour
{
    private ButterBody _body;

    public void Init(ButterBody body) => _body = body;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_body == null) return;
        _body.ReportCollision(collision);
    }
}
