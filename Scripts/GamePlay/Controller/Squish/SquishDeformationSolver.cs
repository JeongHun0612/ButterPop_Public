using System;
using UnityEngine;

// 각 링의 길이/폭 압축률만 계산한다. 뼈, Transform, Collider는 변경하지 않는다.
// 압축은 뼈의 로컬 축을 따르므로 이동/회전 자체가 메시의 지연이나 흔들림을 만들지 않는다.
public sealed class SquishDeformationSolver
{
    private readonly Vector2[] _compression;
    private readonly Vector2[] _previousCompression;
    private readonly Vector2[] _velocity;
    private float _maxCompression;
    private float _dampingRatio;
    private float _frequency;
    private float _impactStrength;

    public SquishDeformationSolver(int ringCount)
    {
        if (ringCount < 2) throw new ArgumentOutOfRangeException(nameof(ringCount));
        _compression = new Vector2[ringCount];
        _previousCompression = new Vector2[ringCount];
        _velocity = new Vector2[ringCount];
        Configure(0.8f, 0.18f, 0f, 1f);
    }

    public void Configure(float softness, float recoveryTime, float rebound, float impactStrength)
    {
        _maxCompression = Mathf.Clamp01(softness) * 0.3f;
        recoveryTime = Mathf.Clamp(recoveryTime, 0.05f, 1f);
        _impactStrength = Mathf.Clamp(impactStrength, 0f, 2f);
        rebound = Mathf.Clamp01(rebound);
        _dampingRatio = Mathf.Lerp(1f, 0.35f, rebound);
        // 임계 감쇠에서 (1 + w*t)*exp(-w*t) = 0.05가 되는 w*t ≈ 4.744.
        // 반동을 추가해도 감쇠 시간의 규모가 크게 바뀌지 않도록 보정한다.
        _frequency = Mathf.Lerp(4.744f, 3f / _dampingRatio, rebound) / recoveryTime;
    }

    public void Reset()
    {
        Array.Clear(_compression, 0, _compression.Length);
        Array.Clear(_previousCompression, 0, _previousCompression.Length);
        Array.Clear(_velocity, 0, _velocity.Length);
    }

    public void AddImpact(Vector2[] goals, Vector2 point, Vector2 normal, float speed)
    {
        if (!ValidGoals(goals) || !IsFinite(point) || !IsFinite(normal)
            || !IsFinite(speed) || speed <= 0f || normal.sqrMagnitude < 0.0001f
            || _maxCompression <= 0f || _impactStrength <= 0f) return;

        normal.Normalize();
        float width = AverageWidth(goals);
        float spacing = AverageSpacing(goals);
        // 몸 크기와 표면 간격을 함께 고려해 접촉점 사이에도 충격이 전달되게 한다.
        float radius = Mathf.Max(width * 0.6f, Mathf.Max(spacing * 1.25f, 0.001f));
        float speedResponse = 1f - Mathf.Exp(-speed / Mathf.Max(width * 12f, 0.01f));
        float amount = _maxCompression * speedResponse * _impactStrength;

        for (int r = 0; r < _compression.Length; r++)
        {
            Vector2 side = (goals[r * 2 + 1] - goals[r * 2]).normalized;
            Vector2 axis = new Vector2(side.y, -side.x);
            Vector2 center = Center(goals, r);
            // 접촉점과 링의 단면 선분 사이 거리. 폭이 커져도 표면 충돌을 놓치지 않는다.
            float halfWidth = Vector2.Distance(goals[r * 2], goals[r * 2 + 1]) * 0.5f;
            Vector2 closest = center + side * Mathf.Clamp(Vector2.Dot(point - center, side), -halfWidth, halfWidth);
            float weight = Mathf.Exp(-(point - closest).sqrMagnitude / (radius * radius));
            float along = Vector2.Dot(normal, axis);
            float across = Vector2.Dot(normal, side);
            Vector2 squeeze = new Vector2(along * along, across * across) * (amount * weight);

            _compression[r] = Limit(_compression[r] + squeeze);
            _velocity[r] *= 1f - weight;
            // 충돌은 즉시 보여 주고 이후 복원 과정만 프레임 사이에서 보간한다.
            _previousCompression[r] = _compression[r];
        }
    }

    public void Step(float dt)
    {
        if (!IsFinite(dt) || dt <= 0f) return;
        for (int r = 0; r < _compression.Length; r++)
        {
            Vector2 position = Limit(_compression[r]);
            Vector2 velocity = _velocity[r];
            _previousCompression[r] = position;

            if (_maxCompression <= 0f || !IsFinite(position) || !IsFinite(velocity))
            {
                _compression[r] = Vector2.zero;
                _previousCompression[r] = Vector2.zero;
                _velocity[r] = Vector2.zero;
                continue;
            }

            // 감쇠 스프링의 해석해. Fixed Timestep이나 제약 반복 횟수에 의존하지 않는다.
            float decay = Mathf.Exp(-_dampingRatio * _frequency * dt);
            if (_dampingRatio >= 0.999f)
            {
                Vector2 coefficient = velocity + _frequency * position;
                _compression[r] = (position + coefficient * dt) * decay;
                _velocity[r] = (velocity - _frequency * coefficient * dt) * decay;
            }
            else
            {
                float dampedFrequency = _frequency * Mathf.Sqrt(1f - _dampingRatio * _dampingRatio);
                float cosine = Mathf.Cos(dampedFrequency * dt);
                float sine = Mathf.Sin(dampedFrequency * dt);
                Vector2 coefficient = (velocity + _dampingRatio * _frequency * position) / dampedFrequency;
                _compression[r] = (position * cosine + coefficient * sine) * decay;
                _velocity[r] = (velocity * cosine
                    - (_dampingRatio * _frequency * velocity + _frequency * _frequency * position)
                    * (sine / dampedFrequency)) * decay;
            }

            _compression[r] = Limit(_compression[r]);
            if (_compression[r].sqrMagnitude < 1e-10f && _velocity[r].sqrMagnitude < 1e-8f)
            {
                _compression[r] = Vector2.zero;
                _velocity[r] = Vector2.zero;
            }
        }
    }

    // 출력 배열은 호출자가 재사용한다. 기준 형태와 시뮬레이션 상태는 수정하지 않는다.
    public void WriteOffsets(Vector2[] goals, Vector2[] offsets, float interpolation)
    {
        if (!ValidGoals(goals) || offsets == null || offsets.Length != goals.Length)
            throw new ArgumentException("목표 정점과 출력 배열은 링 개수의 두 배여야 합니다.");

        float width = AverageWidth(goals);
        float limit = width * _maxCompression;
        Vector2 centerOffset = Vector2.zero;
        Vector2 previousSqueeze = Vector2.zero;
        for (int r = 0; r < _compression.Length; r++)
        {
            Vector2 squeeze = Limit(Vector2.Lerp(_previousCompression[r], _compression[r], interpolation));
            if (r > 0)
            {
                Vector2 average = (previousSqueeze + squeeze) * 0.5f;
                // 폭이 눌리면 길이 방향으로 조금 퍼지고, 끝 충돌은 길이를 압축한다.
                float lengthChange = average.y * 0.35f - average.x;
                centerOffset += (Center(goals, r) - Center(goals, r - 1)) * lengthChange;
                centerOffset = Vector2.ClampMagnitude(centerOffset, limit);
            }

            Vector2 halfSide = (goals[r * 2 + 1] - goals[r * 2]) * 0.5f;
            Vector2 sideOffset = halfSide * (squeeze.x * 0.35f - squeeze.y);
            offsets[r * 2] = Vector2.ClampMagnitude(centerOffset - sideOffset, limit);
            offsets[r * 2 + 1] = Vector2.ClampMagnitude(centerOffset + sideOffset, limit);
            previousSqueeze = squeeze;
        }
    }

    private Vector2 Limit(Vector2 value) => new Vector2(
        Mathf.Clamp(value.x, -_maxCompression * 0.5f, _maxCompression),
        Mathf.Clamp(value.y, -_maxCompression * 0.5f, _maxCompression));

    private bool ValidGoals(Vector2[] goals) => goals != null && goals.Length == _compression.Length * 2;
    private static Vector2 Center(Vector2[] goals, int ring) => (goals[ring * 2] + goals[ring * 2 + 1]) * 0.5f;

    private static float AverageWidth(Vector2[] goals)
    {
        float width = 0f;
        for (int i = 0; i < goals.Length; i += 2) width += Vector2.Distance(goals[i], goals[i + 1]);
        return width / (goals.Length / 2);
    }

    private static float AverageSpacing(Vector2[] goals)
    {
        float length = 0f;
        int rings = goals.Length / 2;
        for (int r = 1; r < rings; r++) length += Vector2.Distance(Center(goals, r), Center(goals, r - 1));
        return length / (rings - 1);
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
}
