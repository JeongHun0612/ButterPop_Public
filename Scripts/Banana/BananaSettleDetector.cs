using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BananaSettleDetector : MonoBehaviour
{
    [Header("판정")]
    [SerializeField] private float settleSpeedThreshold = 0.3f;
    [SerializeField] private float settleTimeRequired = 0.5f;
    [SerializeField] private float outOfBoundsMargin = 2f;

    [Header("딜레이")]
    [SerializeField] private float respawnDelay = 0.5f;

    private Rigidbody2D _rb;
    private float _fallYThreshold;
    private float _fallXThreshold;

    private readonly HashSet<StageObject> _touchingStages = new();
    private bool _touchingMonkeyArm;
    private float _settleTimer;
    private bool _judged;



    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (StageManager.Instance == null)
            return;

        var state = StageManager.Instance.CurrentState;
        if (state != GameState.Falling && state != GameState.Settling)
            return;

        if (_judged)
            return;

        var pos = transform.position;
        if (pos.y < _fallYThreshold || Mathf.Abs(pos.x) > _fallXThreshold)
        {
            OnFailed();
            return;
        }

        if (_touchingStages.Count > 0 && !_touchingMonkeyArm && _rb.linearVelocity.magnitude < settleSpeedThreshold)
        {
            if (state != GameState.Settling)
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

    public void InitializeBounds(Camera cam)
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        _fallYThreshold = cam.transform.position.y - halfH - outOfBoundsMargin;
        _fallXThreshold = halfW + outOfBoundsMargin;
    }

    public void ResetState()
    {
        _judged = false;
        _settleTimer = 0f;
        _touchingStages.Clear();
        _touchingMonkeyArm = false;
    }

    public Bounds GetSettledBounds()
    {
        var bounds = GetRendererBounds(gameObject);

        foreach (var stage in _touchingStages)
        {
            if (stage == null) continue;
            bounds.Encapsulate(GetRendererBounds(stage.gameObject));
        }

        return bounds;
    }

    private void OnSuccess()
    {
        _judged = true;
        StageManager.Instance.BananaController.FreezeInPlace();
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
        StageManager.Instance.ResetBanana();
        Debug.Log("[Result] 바나나 리스폰 완료");
    }

    private static Bounds GetRendererBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.zero);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.GetComponentInParent<MonkeyArm>() != null)
        {
            _touchingMonkeyArm = true;
            return;
        }

        var stage = collision.gameObject.GetComponent<StageObject>();
        if (stage != null)
            _touchingStages.Add(stage);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.GetComponentInParent<MonkeyArm>() != null)
        {
            _touchingMonkeyArm = false;
            return;
        }

        var stage = collision.gameObject.GetComponent<StageObject>();
        if (stage != null)
            _touchingStages.Remove(stage);
    }
}
