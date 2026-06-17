using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BananaSettleDetector : MonoBehaviour
{
    [Header("판정")]
    [SerializeField] private float settleSpeedThreshold = 0.3f;
    [SerializeField] private float settleTimeRequired = 0.5f;
    [SerializeField] private float fallYThreshold = -8f;

    [Header("리스폰")]
    [SerializeField] private float respawnDelay = 1.0f;

    private Rigidbody2D _rb;
    private BananaThrowController _throwController;
    private Vector2 _spawnPosition;

    private readonly HashSet<StageObject> _touchingStages = new();
    private float _settleTimer;
    private bool _judged;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _throwController = GetComponent<BananaThrowController>();
        _spawnPosition = transform.position;
    }

    private void Update()
    {
        if (StageManager.Instance == null)
            return;

        var state = StageManager.Instance.CurrentState;
        if (state != GameState.Flying && state != GameState.Settling)
            return;

        if (_judged)
            return;

        if (transform.position.y < fallYThreshold)
        {
            OnFailed();
            return;
        }

        if (_touchingStages.Count > 0 && _rb.linearVelocity.magnitude < settleSpeedThreshold)
        {
            if (state == GameState.Flying)
                StageManager.Instance.SetState(GameState.Settling);

            _settleTimer += Time.deltaTime;
            if (_settleTimer >= settleTimeRequired)
            {
                OnSuccess();
            }
        }
        else
        {
            _settleTimer = 0f;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        var stage = collision.gameObject.GetComponent<StageObject>();
        if (stage != null)
            _touchingStages.Add(stage);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        var stage = collision.gameObject.GetComponent<StageObject>();
        if (stage != null)
            _touchingStages.Remove(stage);
    }

    public void SetSpawnPosition(Vector2 position)
    {
        _spawnPosition = position;
    }

    public void SetFallYThreshold(float threshold)
    {
        fallYThreshold = threshold;
    }

    private void OnSuccess()
    {
        _judged = true;
        StageManager.Instance.SetState(GameState.Success);
        Debug.Log("<color=green>[Result] SUCCESS - 바나나가 스테이지에 안착했습니다!</color>");
    }

    private void OnFailed()
    {
        _judged = true;
        StageManager.Instance.SetState(GameState.Failed);
        Debug.Log("<color=red>[Result] FAILED - 바나나가 떨어졌습니다!</color>");
        Invoke(nameof(Respawn), respawnDelay);
    }

    private void Respawn()
    {
        _judged = false;
        _settleTimer = 0f;
        _touchingStages.Clear();
        _throwController.ResetBanana(_spawnPosition);
        Debug.Log("[Result] 바나나 리스폰 완료");
    }
}
