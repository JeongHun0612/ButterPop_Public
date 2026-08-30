using UnityEngine;

// SquishBody 홀더 잡기 시스템
public partial class SquishBody
{
    private int GrabbedCount => Mathf.Clamp(grabbedBoneCount, 0, _bones.Count);

    // 외부 홀더에 잡기. 0=힌지 매달림, N=앞 N개 강체 그랩
    public void Grab(Rigidbody2D holder, Vector2 holderWorldAnchor, float angleDeg)
    {
        if (holder == null) return;

        if (GrabbedCount == 0)
        {
            ReleaseGrabbedBones();
            AttachToHolder(holder, holderWorldAnchor);
        }
        else
        {
            DetachFromHolder();
            SetGrabbedBonesKinematic(true);
            DriveGrabbedBones(holderWorldAnchor, angleDeg, false);
        }
    }

    // 매 FixedUpdate 홀더 추적. N=0(힌지 모드)은 물리 전담이라 no-op
    public void UpdateGrab(Vector2 holderWorldAnchor, float angleDeg)
    {
        if (GrabbedCount == 0) return;
        DriveGrabbedBones(holderWorldAnchor, angleDeg, true);
    }

    // 홀더에서 풀어 자유낙하(drop). 두 방식 모두 안전 해제
    public void Release()
    {
        DetachFromHolder();
        ReleaseGrabbedBones();
    }

    // 머리 뼈를 홀더에 HingeJoint로 매달기. 앵커·강성(headSwingLimit) 고정값이라 최초 1회만 설정, 재부착은 enabled 토글
    private void AttachToHolder(Rigidbody2D holder, Vector2 holderWorldAnchor)
    {
        Rigidbody2D head = GetLeadingRigidbody();
        if (head == null || holder == null) return;

        if (_holderJoint == null)
        {
            _holderJoint = head.gameObject.AddComponent<HingeJoint2D>();
            _holderJoint.autoConfigureConnectedAnchor = false;
            _holderJoint.enableCollision = false;
            _holderJoint.connectedBody = holder;
            // 머리 뼈 리딩 에지를 홀더 앵커에 핀
            _holderJoint.anchor = new Vector2(-GetLeadingEdgeToFirstBoneOffset(), 0f);
            _holderJoint.connectedAnchor = holder.transform.InverseTransformPoint(holderWorldAnchor);

            bool limited = headSwingLimit < 90f;
            _holderJoint.useLimits = limited;
            if (limited)
                _holderJoint.limits = new JointAngleLimits2D { min = -headSwingLimit, max = headSwingLimit };
        }

        _holderJoint.enabled = true;
    }

    private void DetachFromHolder()
    {
        if (_holderJoint != null)
            _holderJoint.enabled = false;
    }

    // 앞 N개 뼈 Kinematic↔Dynamic 전환(잡기/놓기)
    private void SetGrabbedBonesKinematic(bool kinematic)
    {
        int n = GrabbedCount;
        var type = kinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        for (int i = 0; i < n; i++)
        {
            var rb = _bones[i].Rigidbody;
            rb.bodyType = type;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    // 앞 N개 뼈를 홀더 프레임 rest 자리에 각도 고정 배치. sweep=true=MovePosition(부드럽게), false=즉시 스냅
    private void DriveGrabbedBones(Vector2 leadingEdgeWorld, float angleDeg, bool sweep)
    {
        int n = GrabbedCount;
        if (n == 0) return;

        float rad = angleDeg * Mathf.Deg2Rad;
        Vector2 axis = (Vector2)(Quaternion.Euler(0f, 0f, angleDeg) * Vector3.right);
        Vector2 origin = leadingEdgeWorld + axis * (length * 0.5f);

        for (int i = 0; i < n; i++)
        {
            var rb = _bones[i].Rigidbody;
            Vector2 position = origin + Rotate(_bones[i].Rest.position, rad);

            if (sweep && rb.bodyType == RigidbodyType2D.Kinematic)
            {
                rb.MovePosition(position);
                rb.MoveRotation(angleDeg);
            }
            else
            {
                rb.position = position;
                rb.rotation = angleDeg;
                rb.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angleDeg));
            }

            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void ReleaseGrabbedBones() => SetGrabbedBonesKinematic(false);

    private Rigidbody2D GetLeadingRigidbody()
    {
        return _bones.Count > 0 ? _bones[0].Rigidbody : null;
    }

    private float GetLeadingEdgeToFirstBoneOffset()
    {
        if (_bones.Count == 0) return 0f;
        return length * 0.5f + _bones[0].Rest.position.x;
    }
}
