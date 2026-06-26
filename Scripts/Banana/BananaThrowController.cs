using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BananaThrowController : MonoBehaviour
{
    [Header("물리")]
    [SerializeField] private float gravityScale = 1.0f;
    [SerializeField] private float linearDrag = 0.1f;
    [SerializeField] private float angularDrag = 0.2f;
    [SerializeField] private float mass = 1.0f;

    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        SetKinematic();
    }

    public void DropBanana()
    {
        SetDynamic();
        StageManager.Instance.SetState(GameState.Falling);
    }

    public void ResetBanana(Vector2 spawnPosition)
    {
        SetKinematic();
        transform.position = (Vector3)spawnPosition;
        transform.rotation = Quaternion.identity;
    }

    private void SetKinematic()
    {
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
    }

    private void SetDynamic()
    {
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = gravityScale;
        _rb.linearDamping = linearDrag;
        _rb.angularDamping = angularDrag;
        _rb.mass = mass;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }
}
