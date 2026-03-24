/// <summary>
/// Sistema de Atração Magnética de Vértices para VR.
/// Dispara uma esfera de colisão (OverlapSphere) a partir do dedo do usuário
/// para capturar vértices próximos, compensando a falta de precisão motora.
/// Suporta pinch simultâneo: cada mão pode modelar um vértice ao mesmo tempo.
/// Integra com VertexHUD para exibir distância durante o arrasto.
/// Expõe IsPinching pra que o ObjectGrab saiba quando pausar o preview.
/// </summary>

using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

public class MagneticSnapping : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Header("Configurações Magnéticas")]
    public float magneticRadius = 0.05f;
    public LayerMask vertexLayer;

    [Header("Dampening (Filtro de Jitter)")]
    [Tooltip("0 = sem movimento, 1 = Sem filtro ( Jitter bruto ). Recomendado: 0.15")]
    [Range(0f, 1f)]
    public float dampeningSpeed = 0.15f;

    private XRHandSubsystem handSubsystem;

    // Estado por mão — cada mão tem seu próprio snap independente
    private Transform snappedLeft;
    private Transform snappedRight;
    private Renderer rendererLeft;
    private Renderer rendererRight;
    private MaterialPropertyBlock propBlockLeft;
    private MaterialPropertyBlock propBlockRight;
    private VertexHUD hudLeft;
    private VertexHUD hudRight;

    // Smoothing separado por mão
    private Vector3 smoothedLeft;
    private Vector3 smoothedRight;
    private bool smoothInitLeft;
    private bool smoothInitRight;

    /// <summary>
    /// Retorna true se qualquer mão está pinçando um vértice.
    /// Usado pelo ObjectGrab pra pausar o preview.
    /// </summary>
    public bool IsPinching => snappedLeft != null || snappedRight != null;

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
        smoothInitLeft = false;
        smoothInitRight = false;
    }

    void Update()
    {
        if (handSubsystem == null || !handSubsystem.running) return;

        // Processa as duas mãos sempre — independente uma da outra
        ProcessHand(handSubsystem.leftHand, true);
        ProcessHand(handSubsystem.rightHand, false);
    }

    private void ProcessHand(XRHand hand, bool isLeft)
    {
        // Referências por mão
        Transform snapped = isLeft ? snappedLeft : snappedRight;

        if (!hand.isTracked)
        {
            if (isLeft) smoothInitLeft = false;
            else smoothInitRight = false;
            return;
        }

        // Pega posições do indicador e do polegar
        bool hasIndex = hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexPose);
        bool hasThumb = hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbPose);

        if (!hasIndex || !hasThumb) return;

        // Smoothing por mão
        if (isLeft)
        {
            if (!smoothInitLeft) { smoothedLeft = indexPose.position; smoothInitLeft = true; }
            smoothedLeft = Vector3.Lerp(smoothedLeft, indexPose.position, dampeningSpeed);
        }
        else
        {
            if (!smoothInitRight) { smoothedRight = indexPose.position; smoothInitRight = true; }
            smoothedRight = Vector3.Lerp(smoothedRight, indexPose.position, dampeningSpeed);
        }

        Vector3 origin = isLeft ? smoothedLeft : smoothedRight;
        float pinchDistance = Vector3.Distance(indexPose.position, thumbPose.position);
        bool isPinching = pinchDistance < 0.03f;

        if (isPinching && snapped == null)
        {
            // Procura vértice pra essa mão
            Transform found = SearchForVertex(origin, isLeft);
            if (isLeft) snappedLeft = found;
            else snappedRight = found;
        }
        else if (!isPinching && snapped != null)
        {
            // Solta o vértice dessa mão
            ReleaseVertex(isLeft);
        }
        else if (isPinching && snapped != null)
        {
            // Mantém o snap dessa mão
            MaintainSnap(origin, isLeft);
        }
    }

    private Transform SearchForVertex(Vector3 origin, bool isLeft)
    {
        Collider[] hits = Physics.OverlapSphere(origin, magneticRadius, vertexLayer);

        if (hits.Length > 0)
        {
            // Evita que as duas mãos peguem a mesma sphere
            Transform other = isLeft ? snappedRight : snappedLeft;
            foreach (Collider hit in hits)
            {
                if (hit.transform == other) continue; // já pegada pela outra mão

                Transform vertex = hit.transform;
                Renderer rend = vertex.GetComponent<Renderer>();
                MaterialPropertyBlock prop = new MaterialPropertyBlock();
                VertexHUD hud = vertex.GetComponent<VertexHUD>();

                if (isLeft)
                {
                    rendererLeft = rend;
                    propBlockLeft = prop;
                    hudLeft = hud;
                }
                else
                {
                    rendererRight = rend;
                    propBlockRight = prop;
                    hudRight = hud;
                }

                SetVertexColor(rend, prop, Color.green);
                if (hud != null) hud.Show();
                Debug.DrawLine(origin, vertex.position, Color.green);
                return vertex;
            }
        }

        DrawRedCross(origin);
        return null;
    }

    private void MaintainSnap(Vector3 origin, bool isLeft)
    {
        Transform snapped = isLeft ? snappedLeft : snappedRight;
        VertexHUD hud = isLeft ? hudLeft : hudRight;

        if (snapped == null) return;
        snapped.position = origin;
        if (hud != null) hud.UpdateHUD();
        Debug.DrawLine(origin, snapped.position, Color.green);
    }

    private void ReleaseVertex(bool isLeft)
    {
        if (isLeft)
        {
            ClearVertexColor(rendererLeft);
            if (hudLeft != null) hudLeft.Hide();
            hudLeft = null;
            snappedLeft = null;
            rendererLeft = null;
            propBlockLeft = null;
        }
        else
        {
            ClearVertexColor(rendererRight);
            if (hudRight != null) hudRight.Hide();
            hudRight = null;
            snappedRight = null;
            rendererRight = null;
            propBlockRight = null;
        }
    }

    private void DrawRedCross(Vector3 pos)
    {
        float d = magneticRadius;
        Debug.DrawLine(pos - Vector3.up * d, pos + Vector3.up * d, Color.red);
        Debug.DrawLine(pos - Vector3.right * d, pos + Vector3.right * d, Color.red);
        Debug.DrawLine(pos - Vector3.forward * d, pos + Vector3.forward * d, Color.red);
    }

    private void SetVertexColor(Renderer rend, MaterialPropertyBlock prop, Color color)
    {
        if (rend == null || prop == null) return;
        prop.SetColor(BaseColorId, color);
        rend.SetPropertyBlock(prop);
    }

    private void ClearVertexColor(Renderer rend)
    {
        if (rend == null) return;
        rend.SetPropertyBlock(null);
    }
}
