using UnityEngine;

// 세그먼트 각각의 충돌을 부모 SquishBody로 올려주는 릴레이.
[RequireComponent(typeof(Rigidbody2D))]
public class SquishSegmentRelay : MonoBehaviour
{
    private SquishBody _body;

    public void Init(SquishBody body) => _body = body;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (_body == null) return;
        _body.ReportCollisionEnter(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (_body == null) return;
        _body.ReportCollisionExit(collision);
    }
}
