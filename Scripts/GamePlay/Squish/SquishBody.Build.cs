using UnityEngine;

// SquishBody 런타임 뼈 체인 구축(팩토리)
public partial class SquishBody
{
    private void Build()
    {
        CreateRuntimeBones();

        for (int i = 0; i < _bones.Count; i++)
            for (int j = i + 1; j < _bones.Count; j++)
                Physics2D.IgnoreCollision(_bones[i].Collider, _bones[j].Collider);

        ApplyBendLimits();
        _isReady = true;
    }

    private void CreateRuntimeBones()
    {
        _isReady = false;
        ClearBoneCaches();

        SpineSample[] rest = BuildRestSamples();
        int n = rest.Length;
        var mat = new PhysicsMaterial2D("Squishy") { bounciness = bounciness, friction = friction };
        float boneMass = totalMass / n;

        for (int i = 0; i < n; i++)
        {
            var go = new GameObject($"RuntimeBone_{i}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = rest[i].position;

            var rb = go.AddComponent<Rigidbody2D>();
            var col = CreateBoneCollider(go, rest[i].halfWidth);
            var relay = go.AddComponent<SquishSegmentRelay>();

            ConfigureBoneRigidbody(rb, boneMass);
            col.sharedMaterial = mat;
            relay.Init(this);

            _bones.Add(new Bone
            {
                Rigidbody = rb,
                Collider = col,
                Rest = rest[i]
            });
            _rigidbodies.Add(rb);
            _restSamples.Add(rest[i]);
        }

        for (int i = 1; i < n; i++)
        {
            var hinge = _bones[i].Rigidbody.gameObject.AddComponent<HingeJoint2D>();
            ConfigureHinge(hinge, _bones[i - 1].Rigidbody, rest[i], rest[i - 1]);
            _bones[i].Hinge = hinge;
        }
    }

    // 뼈 콜라이더 1개 생성. Circle·Box 모두 반지름 r로 동일 범위, 형태만 다름
    private Collider2D CreateBoneCollider(GameObject go, float halfWidth)
    {
        float r = Mathf.Max(halfWidth, 0.05f);

        if (colliderShape == ColliderShape.Box)
        {
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(2f * r, 2f * r);
            return box;
        }

        var circle = go.AddComponent<CircleCollider2D>();
        circle.radius = r;
        return circle;
    }

    private void ConfigureBoneRigidbody(Rigidbody2D rb, float mass)
    {
        rb.mass = mass;
        rb.gravityScale = gravityScale;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void ApplyBendLimits()
    {
        var limits = new JointAngleLimits2D { min = -maxBendAnglePerJoint, max = maxBendAnglePerJoint };
        for (int i = 0; i < _bones.Count; i++)
            if (_bones[i].Hinge != null)
            {
                _bones[i].Hinge.useLimits = true;
                _bones[i].Hinge.enableCollision = false;
                _bones[i].Hinge.limits = limits;
            }
    }

    private void ClearBoneCaches()
    {
        _bones.Clear();
        _rigidbodies.Clear();
        _restSamples.Clear();
    }

    private SpineSample[] BuildRestSamples()
    {
        int n = Mathf.Max(boneCount, 2);
        float half = width * 0.5f;

        float lo = -length * 0.5f + half;
        float hi = length * 0.5f - half;
        if (hi < lo)
        {
            float mid = 0.5f * (lo + hi);
            lo = hi = mid;
        }

        var arr = new SpineSample[n];
        for (int i = 0; i < n; i++)
        {
            float x = Mathf.Lerp(lo, hi, i / (float)(n - 1));
            arr[i] = new SpineSample { position = new Vector2(x, 0f), halfWidth = half };
        }
        return arr;
    }

    private static void ConfigureHinge(HingeJoint2D hinge, Rigidbody2D connected, SpineSample a, SpineSample b)
    {
        Vector2 pa = a.position;
        Vector2 pb = b.position;
        Vector2 mid = 0.5f * (pa + pb);

        hinge.connectedBody = connected;
        hinge.autoConfigureConnectedAnchor = false;
        hinge.anchor = mid - pa;
        hinge.connectedAnchor = mid - pb;
        hinge.enableCollision = false;
        hinge.useLimits = true;
    }
}
