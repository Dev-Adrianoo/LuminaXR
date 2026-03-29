using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

/// <summary>
/// Detecta gesto de mão fechada (fist) para segurar e rotacionar o objeto.
/// Suporta ambas as mãos com atribuição dinâmica: a primeira mão que fechar
/// vira a mão de segurar. Mesma mão fecha de novo → solta.
/// Pausa o preview (bob + rotação) quando o MagneticSnapping está pinçando.
/// </summary>
public class ObjectGrab : MonoBehaviour
{
    private XRHandSubsystem handSubsystem;
    private bool isGrabbing;
    private MagneticSnapping magneticSnapping;
    private bool wasFistLeft;
    private bool wasFistRight;
    private bool grabbedByLeft;

    [Header("Configurações de Grab")]
    public float grabRange = 0.15f;
    public float floatHeight = 0.1f;

    [Header("Preview")]
    public float rotateSpeed = 45f;
    public float bobHeight = 0.02f;
    public float bobSpeed = 1.5f;

    void Update()
    {
        if (handSubsystem == null || !handSubsystem.running) return;

        // Verifica as duas mãos pra detectar fist
        CheckHand(handSubsystem.leftHand, ref wasFistLeft, true);
        CheckHand(handSubsystem.rightHand, ref wasFistRight, false);

        // Se está segurando, acompanha a mão que segurou
        if (isGrabbing)
        {
            XRHand activeHand = grabbedByLeft ? handSubsystem.leftHand : handSubsystem.rightHand;
            if (!activeHand.isTracked) return;

            bool hasPalm = activeHand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose);
            if (!hasPalm) return;

            // Segue a palma sempre, mas só faz preview se não estiver pinçando
            transform.position = palmPose.position + Vector3.up * floatHeight;

            bool isPinching = magneticSnapping != null && magneticSnapping.IsPinching;
            if (!isPinching)
            {
                float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                transform.position += Vector3.up * bob;
                transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
            }
        }
    }

    // Verifica se uma mão específica fez o gesto de fist
    private void CheckHand(XRHand hand, ref bool wasFist, bool isLeft)
    {
        if (!hand.isTracked)
        {
            wasFist = false;
            return;
        }

        bool hasPalm = hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose);
        bool hasIndex = hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexPose);
        bool hasMiddle = hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out Pose middlePose);
        bool hasRing = hand.GetJoint(XRHandJointID.RingTip).TryGetPose(out Pose ringPose);
        bool hasLittle = hand.GetJoint(XRHandJointID.LittleTip).TryGetPose(out Pose littlePose);

        if (!hasPalm || !hasIndex || !hasMiddle || !hasRing || !hasLittle)
        {
            wasFist = false;
            return;
        }

        float indexDistance = Vector3.Distance(indexPose.position, palmPose.position);
        float middleDistance = Vector3.Distance(middlePose.position, palmPose.position);
        float ringDistance = Vector3.Distance(ringPose.position, palmPose.position);
        float littleDistance = Vector3.Distance(littlePose.position, palmPose.position);

        bool isFist = indexDistance < 0.07f && middleDistance < 0.07f && ringDistance < 0.07f && littleDistance < 0.07f;

        float distanceToObject = Vector3.Distance(palmPose.position, transform.position);
        bool handIsClose = distanceToObject < grabRange;

        if (isFist && !wasFist && handIsClose)
        {
            if (!isGrabbing)
            {
                // Nenhuma mão segurando → essa mão pega
                isGrabbing = true;
                grabbedByLeft = isLeft;
                var rb = GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;
            }
            else if (grabbedByLeft == isLeft)
            {
                // Mesma mão fecha de novo → solta
                isGrabbing = false;
                var rb = GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = false;
            }
        }

        wasFist = isFist;
    }

    void OnEnable()
    {
        var SubSystems = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(SubSystems);

        if (SubSystems.Count > 0)
        {
            handSubsystem = SubSystems[0];
            handSubsystem.Start();
        }
        else
        {
            Debug.LogError("[ObjectGrab] Nenhum XRHandSubsystem encontrado. " +
                          "Verifique se Hand Tracking Subsystem está ativo no OpenXR.");
        }

        // Busca o MagneticSnapping na cena pra saber quando pausar o preview
        magneticSnapping = FindAnyObjectByType<MagneticSnapping>();
    }

    void OnDisable()
    {
        handSubsystem?.Stop();
    }
}
