using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

/// <summary>
/// Detecta snap de dedos (polegar + médio se tocam e separam rápido) para ligar e desligar gravidade no objeto alvo.
/// Mão direita liga gravidade. Mão esquerda desliga.
/// </summary>
public class SnapGravity : MonoBehaviour
{
    [SerializeField] private Rigidbody _target;

    private XRHandSubsystem _handSubsystem;

    public bool IsSnapping { get; private set; }

    private bool _rightWasPinching;
    private bool _leftWasPinching;

    private static readonly float PinchThreshold = 0.02f;
    private static readonly float SnapReleaseThreshold = 0.025f;

    private void OnEnable()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        _handSubsystem = subsystems.Count > 0 ? subsystems[0] : null;
        if (_handSubsystem != null)
            _handSubsystem.updatedHands += OnUpdatedHands;
    }

    private void OnDisable()
    {
        if (_handSubsystem != null)
            _handSubsystem.updatedHands -= OnUpdatedHands;
    }

    private void OnUpdatedHands(XRHandSubsystem subsystem, XRHandSubsystem.UpdateSuccessFlags flags, XRHandSubsystem.UpdateType updateType)
    {
        if (_target == null) return;
        CheckSnap(subsystem.rightHand, ref _rightWasPinching, enableGravity: true);
        CheckSnap(subsystem.leftHand, ref _leftWasPinching, enableGravity: false);
        IsSnapping = _rightWasPinching || _leftWasPinching;
    }

    private void CheckSnap(XRHand hand, ref bool wasPinching, bool enableGravity)
    {
        if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var thumbPose)) return;
        if (!hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out var middlePose)) return;
        if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out var palmPose)) return;

        float distance = Vector3.Distance(thumbPose.position, middlePose.position);
        float middleToPalm = Vector3.Distance(middlePose.position, palmPose.position);
        bool isPinching = distance < PinchThreshold && middleToPalm < 0.08f;

        if (wasPinching && distance > SnapReleaseThreshold)
        {
            _target.useGravity = enableGravity;
            _target.linearVelocity = Vector3.zero;

            if (!enableGravity)
            {
                _target.linearDamping = 5f;
                _target.AddForce(Vector3.up * 1.5f, ForceMode.Impulse);
            }
            else
            {
                _target.linearDamping = 0f;
            }
        }

        wasPinching = isPinching;
    }
}
