using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

/// <summary>
/// Detecta gesto de pinch no ar (indicador + polegar) e rotaciona o objeto alvo
/// como um trackball virtual. Delta horizontal da mão → eixo Y, delta vertical → eixo X.
/// Mão direita tem prioridade quando ambas estão ativas simultaneamente.
/// </summary>
public class WristRotation : MonoBehaviour
{
    [Header("Detecção")]
    public float fingerPinchThreshold = 0.04f;
    public float fingerReleaseThreshold = 0.06f;
    public float rotationRange = 0.30f;

    [Header("Rotação")]
    public float rotationSpeed = 180f;
    public float rotationSensitivity = 0.15f;

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

    private static readonly int VertexTargetMask = 1 << 3; // Layer VertexTarget = 3

    /// <summary>
    /// Mão direita tem prioridade. Esquerda só é ativa quando direita não está pinçando.
    /// </summary>
    public bool IsActiveForHand(bool isLeft)
    {
        if (!isLeft) return _isPinchingRight;
        return _isPinchingLeft && !_isPinchingRight;
    }

    void OnEnable()
    {
        var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
        {
            _handSubsystem = subsystems[0];
            _handSubsystem.Start();
        }
        else
        {
            Debug.LogError("[WristRotation] XRHandSubsystem não encontrado.");
        }

        if (target != null)
            _rb = target.GetComponent<Rigidbody>();
    }

    void OnDisable()
    {
        _handSubsystem?.Stop();
    }

    /// <summary>
    /// Verifica se o gesto de pinch está ativo para uma mão.
    /// Hysteresis: ativa em fingerPinchThreshold, desativa em fingerReleaseThreshold.
    /// </summary>
    private void CheckGesture(
        XRHand hand,
        ref bool isPinching,
        ref bool firstFrame,
        ref Vector3 lastPinchPoint)
    {
        if (!hand.isTracked) { isPinching = false; return; }

        bool hasIndex  = hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexPose);
        bool hasMiddle = hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out Pose middlePose);
        bool hasThumb  = hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbPose);
        bool hasPalm   = hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose);

        if (!hasIndex || !hasMiddle || !hasThumb || !hasPalm) { isPinching = false; return; }

        Vector3 pinchPoint      = (indexPose.position + middlePose.position) * 0.5f;
        float   thumbIndexDist  = Vector3.Distance(indexPose.position, thumbPose.position);
        float   distToTarget    = target != null ? Vector3.Distance(palmPose.position, target.position) : float.MaxValue;
        bool    handInRange     = distToTarget < rotationRange;
        bool    vertexNearby    = Physics.CheckSphere(pinchPoint, 0.02f, VertexTargetMask);

        if (isPinching)
        {
            if (thumbIndexDist > fingerReleaseThreshold || !handInRange || vertexNearby)
            {
                isPinching = false;
            }
        }
        else
        {
            if (thumbIndexDist < fingerPinchThreshold && handInRange && !vertexNearby)
            {
                isPinching = true;
                firstFrame = true;
                lastPinchPoint = pinchPoint;
            }
        }
    }

    /// <summary>
    /// Aplica rotação trackball ao target com base no delta de posição do pinch.
    /// Delta horizontal (X) → rotação em Y. Delta vertical (Y) → rotação em X.
    /// </summary>
    private void ApplyRotation(Vector3 currentPinchPoint, ref Vector3 lastPinchPoint, ref bool firstFrame)
    {
        if (firstFrame)
        {
            lastPinchPoint = currentPinchPoint;
            firstFrame = false;
            return;
        }

        Vector3 rawDelta    = currentPinchPoint - lastPinchPoint;
        Vector3 smoothDelta = Vector3.Lerp(Vector3.zero, rawDelta, rotationSensitivity);

        float rotateY = smoothDelta.x * rotationSpeed;
        float rotateX = smoothDelta.y * rotationSpeed;

        target.Rotate(rotateX, rotateY, 0f, Space.World);

        lastPinchPoint = currentPinchPoint;
    }

    private Vector3 GetPinchPoint(XRHand hand, Vector3 fallback)
    {
        bool hasIndex  = hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexPose);
        bool hasMiddle = hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out Pose middlePose);
        if (!hasIndex || !hasMiddle) return fallback;
        return (indexPose.position + middlePose.position) * 0.5f;
    }

    void Update()
    {
        if (_handSubsystem == null || !_handSubsystem.running || target == null) return;

        CheckGesture(_handSubsystem.rightHand, ref _isPinchingRight, ref _firstFrameRight, ref _lastPinchRight);
        CheckGesture(_handSubsystem.leftHand,  ref _isPinchingLeft,  ref _firstFrameLeft,  ref _lastPinchLeft);

        if (_rb != null)
            _rb.isKinematic = _isPinchingRight || _isPinchingLeft;

        // Mão direita tem prioridade
        if (_isPinchingRight)
        {
            Vector3 pinchPoint = GetPinchPoint(_handSubsystem.rightHand, _lastPinchRight);
            ApplyRotation(pinchPoint, ref _lastPinchRight, ref _firstFrameRight);
        }
        else if (_isPinchingLeft)
        {
            Vector3 pinchPoint = GetPinchPoint(_handSubsystem.leftHand, _lastPinchLeft);
            ApplyRotation(pinchPoint, ref _lastPinchLeft, ref _firstFrameLeft);
        }
    }
}
