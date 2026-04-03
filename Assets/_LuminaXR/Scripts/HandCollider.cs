using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

/// <summary>
/// Cria colliders kinematic nas palmas das mãos para empurrar o cubo fisicamente.
/// Ativo apenas quando a mão está em modo Neutral (sem gesto ativo).
/// Usa Rigidbody.MovePosition para evitar injeção de velocidade pelo jitter do hand tracking.
/// </summary>
public class HandCollider : MonoBehaviour
{
    [SerializeField] private float _colliderRadius = 0.04f;
    [SerializeField] private float _maxPushSpeed = 0.5f;

    private XRHandSubsystem _handSubsystem;
    private HandModeManager _modeManager;

    private Rigidbody _rbLeft;
    private Rigidbody _rbRight;
    private Collider _colliderLeft;
    private Collider _colliderRight;


    private void Start()
    {
        _modeManager = HandModeManager.Instance;
    }

    private void OnEnable()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        _handSubsystem = subsystems.Count > 0 ? subsystems[0] : null;

        _rbLeft = CreatePalmCollider("LeftPalmCollider");
        _rbRight = CreatePalmCollider("RightPalmCollider");
        _colliderLeft = _rbLeft.GetComponent<Collider>();
        _colliderRight = _rbRight.GetComponent<Collider>();
    }

    private Rigidbody CreatePalmCollider(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);

        var col = go.AddComponent<SphereCollider>();
        col.radius = _colliderRadius;
        col.enabled = false;

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        return rb;
    }

    private void FixedUpdate()
    {
        if (_handSubsystem == null || _modeManager == null) return;

        UpdateHand(_handSubsystem.leftHand, _rbLeft, _colliderLeft, isLeft: true);
        UpdateHand(_handSubsystem.rightHand, _rbRight, _colliderRight, isLeft: false);
    }

    private void UpdateHand(XRHand hand, Rigidbody rb, Collider col, bool isLeft)
    {
        if (!hand.isTracked)
        {
            col.enabled = false;
            return;
        }

        if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose))
        {
            col.enabled = false;
            return;
        }

        bool isNeutral = _modeManager.GetMode(isLeft) == HandMode.Neutral;
        col.enabled = isNeutral;

        if (isNeutral)
        {
            rb.MovePosition(palmPose.position);
            ClampNearbyRigidbodies(palmPose.position);
        }
    }

    private void ClampNearbyRigidbodies(Vector3 center)
    {
        var hits = Physics.OverlapSphere(center, _colliderRadius * 1.5f);
        foreach (var hit in hits)
        {
            var hitRb = hit.attachedRigidbody;
            if (hitRb == null || hitRb.isKinematic) continue;
            if (hitRb.linearVelocity.magnitude > _maxPushSpeed)
                hitRb.linearVelocity = hitRb.linearVelocity.normalized * _maxPushSpeed;
            hitRb.angularVelocity = Vector3.zero;
            // hitRb.useGravity = true; // ativar quando houver chão na cena
        }
    }
}
