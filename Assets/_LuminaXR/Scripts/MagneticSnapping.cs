/// <summary> 
/// /// Sistema de Atração Magnética de Vértices para VR. 
/// /// Dispara uma esfera de colisão (ShepereCast) a partir do dedo do usuário 
/// /// para capturar vértices próximos, compensando a falta de precisão motora. 
/// /// </summary>

using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

public class MagneticSnapping : MonoBehaviour
{
    [Header("Configurações Magnéticas")]
    public float magneticRadius = 0.05f;
    public LayerMask vertexLayer;

    [Header("Dempening (Filtro de Jitter)")]
    [Tooltip("0 = sem movimento, 1 = Sem filtro ( Jitter bruto ). Recomendado: 0.15")]
    [Range(0f, 1f)]
    public float dampeningSpeed = 0.15f;

    private XRHandSubsystem handSubsystem;
    private Transform snappedVertex = null;

    // Guarda a posição suavizada do dedo - separada da posição bruta do SDK
    private Vector3 smoothedFingerPosition;
    private bool smoothingInitialized = false;


    void OnEnable()
    {
        var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        if (subsystems.Count > 0)
        {
            handSubsystem = subsystems[0];
            handSubsystem.Start();
            Debug.Log("[MagneticSnapping] XRHandSubsystem encontrado e iniciado.");
        }
        else
        {
            Debug.LogError("[MagneticSnapping] Nenhum XRHandSubsystem encontrado. " +
                           "Verifique se Hand Tracking Subsystem está ativo no OpenXR.");
        }
    }

    void OnDisable()
    {
        handSubsystem?.Stop();
        smoothingInitialized = false;
    }

    void Update()
    {
        if (handSubsystem == null || !handSubsystem.running) return;

        XRHand hand = handSubsystem.rightHand;
        if (!hand.isTracked)
        {
            //Reseta o smoothing quando perde o tracking.
            // Evita o dedo "Voar" de volta de uma posição antiga ao reaparecer.
            smoothingInitialized = false;
            return;
        }


        // Pega posições do indicador e do polegar
        bool hasIndex = hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexPose);
        bool hasThumb = hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbPose);

        if (!hasIndex || !hasThumb) return;


        if (!smoothingInitialized)
        {
            smoothedFingerPosition = indexPose.position;
            smoothingInitialized = true;
        }


        // Learp = interpolação linear entre dois pontos
        // A cada frame, move smoothedFingerPosition X% em direção á posição real
        // Com dampenSpeed 0.15: MOVE 15% DA DISTANCIA RESTANTE, FICANDO MAIS LENTO QUANTO MAIS PERTO ESTIVER
        // resultado: movimento rápidos ficam suavizados, movimentos lentos são precisos

        smoothedFingerPosition = Vector3.Lerp(
            smoothedFingerPosition, // de onde estou
            indexPose.position, // para onde quero ir (posição bruta do SDK)
            dampeningSpeed // quão rápido chego lá
            );

        Vector3 origin = smoothedFingerPosition;
        float pinchDistance = Vector3.Distance(indexPose.position, thumbPose.position);
        bool isPinching = pinchDistance < 0.03f; // 3cm = gesto de pinça

        if (isPinching && snappedVertex == null)
            SearchForVertex(origin);
        else if (!isPinching && snappedVertex != null)
            snappedVertex = null; // Solta ao abrir a mão
        else if (isPinching && snappedVertex != null)
            MaintainSnap(origin);
    }

    private void SearchForVertex(Vector3 origin)
    {
        Collider[] hits = Physics.OverlapSphere(origin, magneticRadius, vertexLayer);

        if (hits.Length > 0)
        {
            snappedVertex = hits[0].transform;
            Debug.DrawLine(origin, snappedVertex.position, Color.green);
        }
        else
        {
            DrawRedCross(origin);
        }
    }

    private void MaintainSnap(Vector3 origin)
    {
        if (snappedVertex == null) return;
        snappedVertex.position = origin;
        Debug.DrawLine(origin, snappedVertex.position, Color.green);
    }

    private void DrawRedCross(Vector3 pos)
    {
        float d = magneticRadius;
        Debug.DrawLine(pos - Vector3.up * d, pos + Vector3.up * d, Color.red);
        Debug.DrawLine(pos - Vector3.right * d, pos + Vector3.right * d, Color.red);
        Debug.DrawLine(pos - Vector3.forward * d, pos + Vector3.forward * d, Color.red);
    }
}