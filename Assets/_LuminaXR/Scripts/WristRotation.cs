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
    public float rotationDamping = 0.15f;

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
}
