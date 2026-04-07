using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

/// Detecta pinch (indicador + polegar) e rotaciona o objeto alvo usando a orientacao
/// da palma como trackball. Ao soltar, momentum continua a rotacao com drag.
public class WristRotation : MonoBehaviour
{
    [Header("Detecção")]
    public float fingerPinchThreshold = 0.008f;
    public float fingerReleaseThreshold = 0.015f;
    public float rotationRange = 0.30f;

    [Header("Momentum")]
    public float momentumDrag = 5.0f;
    public float momentumMinSpeed = 0.5f;

    [Header("Referência")]
    public Transform target;

    private XRHandSubsystem _handSubsystem;
    private Rigidbody _rb;

    private bool _isPinchingLeft;
    private bool _isPinchingRight;
    private bool _firstFrameLeft;
    private bool _firstFrameRight;
    private Vector3 _lastPinchLeft;
    private Vector3 _lastPinchRight;
    private float _pinchStartDistLeft;
    private float _pinchStartDistRight;

    private Quaternion _lastPalmRotationLeft;
    private Quaternion _lastPalmRotationRight;

    private Vector3 _angularVelocity;
    private Vector3[] _velocityBuffer = new Vector3[3];
    private int _velocityIndex;

    private bool _leftTracked;
    private bool _rightTracked;
    private Pose _leftIndexPose, _leftMiddlePose, _leftThumbPose, _leftPalmPose;
    private Pose _rightIndexPose, _rightMiddlePose, _rightThumbPose, _rightPalmPose;

    private static readonly int VertexTargetMask = 1 << 3;

    public bool IsActiveForHand(bool isLeft)
    {
        if (!isLeft) return _isPinchingRight;
        return _isPinchingLeft && !_isPinchingRight;
    }

    public bool HasMomentum => _angularVelocity.sqrMagnitude > momentumMinSpeed * momentumMinSpeed;

    void OnEnable()
    {
        var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
        {
            _handSubsystem = subsystems[0];
            _handSubsystem.updatedHands += OnUpdatedHands;
            _handSubsystem.Start();
        }
        else
        {
            Debug.LogError("[WristRotation] XRHandSubsystem nao encontrado.");
        }

        if (target != null)
            _rb = target.GetComponent<Rigidbody>();
    }

    void OnDisable()
    {
        if (_handSubsystem != null)
            _handSubsystem.updatedHands -= OnUpdatedHands;
        _handSubsystem?.Stop();
        _firstFrameLeft = false;
        _firstFrameRight = false;
        _angularVelocity = Vector3.zero;
    }

    private void OnUpdatedHands(XRHandSubsystem subsystem,
        XRHandSubsystem.UpdateSuccessFlags flags,
        XRHandSubsystem.UpdateType updateType)
    {
        if (subsystem.rightHand.isTracked)
        {
            bool allValid = subsystem.rightHand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var rIndex)
                          & subsystem.rightHand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out var rMiddle)
                          & subsystem.rightHand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var rThumb)
                          & subsystem.rightHand.GetJoint(XRHandJointID.Palm).TryGetPose(out var rPalm);

            if (allValid)
            {
                _rightTracked = true;
                _rightIndexPose = rIndex;
                _rightMiddlePose = rMiddle;
                _rightThumbPose = rThumb;
                _rightPalmPose = rPalm;
            }
            else
            {
                _rightTracked = false;
            }
        }
        else
        {
            _rightTracked = false;
        }

        if (subsystem.leftHand.isTracked)
        {
            bool allValid = subsystem.leftHand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var lIndex)
                          & subsystem.leftHand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out var lMiddle)
                          & subsystem.leftHand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out var lThumb)
                          & subsystem.leftHand.GetJoint(XRHandJointID.Palm).TryGetPose(out var lPalm);

            if (allValid)
            {
                _leftTracked = true;
                _leftIndexPose = lIndex;
                _leftMiddlePose = lMiddle;
                _leftThumbPose = lThumb;
                _leftPalmPose = lPalm;
            }
            else
            {
                _leftTracked = false;
            }
        }
        else
        {
            _leftTracked = false;
        }
    }

    private void CheckGesture(
        bool isTracked,
        Pose indexPose,
        Pose middlePose,
        Pose thumbPose,
        Pose palmPose,
        bool isLeftHand,
        ref bool isPinching,
        ref bool firstFrame,
        ref Vector3 lastPinchPoint)
    {
        if (!isTracked) { isPinching = false; return; }

        Vector3 pinchPoint      = (indexPose.position + middlePose.position) * 0.5f;
        float   thumbIndexDist  = Vector3.Distance(indexPose.position, thumbPose.position);
        float   distToTarget    = target != null ? Vector3.Distance(palmPose.position, target.position) : float.MaxValue;
        bool    handInRange     = distToTarget < rotationRange;
        bool    vertexNearby    = Physics.CheckSphere(pinchPoint, 0.02f, VertexTargetMask);

        if (isPinching)
        {
            bool distanceShifted = Mathf.Abs(distToTarget - (isLeftHand ? _pinchStartDistLeft : _pinchStartDistRight)) > 0.15f;
            if (thumbIndexDist > fingerReleaseThreshold || !handInRange || distanceShifted || vertexNearby)
            {
                isPinching = false;
            }
        }
        else
        {
            if (HandModeManager.Instance != null)
            {
                HandMode mode = HandModeManager.Instance.GetMode(isLeftHand);
                if (mode != HandMode.Neutral && mode != HandMode.Rotate) return;
            }

            if (thumbIndexDist < fingerPinchThreshold && handInRange && !vertexNearby)
            {
                isPinching = true;
                firstFrame = true;
                lastPinchPoint = pinchPoint;
                _angularVelocity = Vector3.zero;
                if (isLeftHand) _pinchStartDistLeft = distToTarget;
                else _pinchStartDistRight = distToTarget;
            }
        }
    }

    private void ApplyWristRotation(Quaternion currentPalmRot, ref Quaternion lastPalmRot, ref bool firstFrame)
    {
        if (firstFrame)
        {
            lastPalmRot = currentPalmRot;
            firstFrame = false;
            for (int i = 0; i < _velocityBuffer.Length; i++)
                _velocityBuffer[i] = Vector3.zero;
            _velocityIndex = 0;
            return;
        }

        Quaternion deltaRot = currentPalmRot * Quaternion.Inverse(lastPalmRot);
        deltaRot.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f) angle -= 360f;

        if (Mathf.Abs(angle) > 0.01f && axis.sqrMagnitude > 0.5f)
        {
            target.rotation = deltaRot * target.rotation;

            Vector3 frameVelocity = axis.normalized * (angle / Time.deltaTime);
            _velocityBuffer[_velocityIndex] = frameVelocity;
            _velocityIndex = (_velocityIndex + 1) % _velocityBuffer.Length;
        }

        lastPalmRot = currentPalmRot;
    }

    private Vector3 SmoothedAngularVelocity()
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < _velocityBuffer.Length; i++)
            sum += _velocityBuffer[i];
        return sum / _velocityBuffer.Length;
    }

    void Update()
    {
        if (_handSubsystem == null || target == null) return;

        bool wasPinching = _isPinchingRight || _isPinchingLeft;

        CheckGesture(
            _rightTracked,
            _rightIndexPose, _rightMiddlePose, _rightThumbPose, _rightPalmPose,
            false, ref _isPinchingRight, ref _firstFrameRight, ref _lastPinchRight);
        CheckGesture(
            _leftTracked,
            _leftIndexPose, _leftMiddlePose, _leftThumbPose, _leftPalmPose,
            true, ref _isPinchingLeft, ref _firstFrameLeft, ref _lastPinchLeft);

        bool isPinching = _isPinchingRight || _isPinchingLeft;

        if (wasPinching && !isPinching)
            _angularVelocity = SmoothedAngularVelocity();

        if (_isPinchingRight)
        {
            ApplyWristRotation(_rightPalmPose.rotation, ref _lastPalmRotationRight, ref _firstFrameRight);
        }
        else if (_isPinchingLeft)
        {
            ApplyWristRotation(_leftPalmPose.rotation, ref _lastPalmRotationLeft, ref _firstFrameLeft);
        }
        else if (HasMomentum)
        {
            target.Rotate(_angularVelocity * Time.deltaTime, Space.World);
            _angularVelocity *= 1f - momentumDrag * Time.deltaTime;

            if (_angularVelocity.magnitude < momentumMinSpeed)
                _angularVelocity = Vector3.zero;
        }

        if (_rb != null)
            _rb.isKinematic = isPinching || HasMomentum;
    }
}
