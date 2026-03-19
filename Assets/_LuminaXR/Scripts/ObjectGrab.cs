using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

public class ObjectGrab : MonoBehaviour
{
    private XRHandSubsystem handSubsystem;
    private bool isGrabbing;
    private bool wasFist;

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

        XRHand hand = handSubsystem.rightHand;
        if(!hand.isTracked) return;

        bool hasPalm = hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose);
        bool hasIndex = hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexPose);
        bool hasMiddle = hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out Pose middlePose);
        bool hasRing = hand.GetJoint(XRHandJointID.RingTip).TryGetPose(out Pose ringPose);
        bool hasLittle = hand.GetJoint(XRHandJointID.LittleTip).TryGetPose(out Pose littlePose);

        if (!hasPalm || !hasIndex || !hasMiddle || !hasRing || !hasLittle) return;
    
        float indexDistance = Vector3.Distance(indexPose.position, palmPose.position);
        float middleDistance = Vector3.Distance(middlePose.position, palmPose.position);
        float ringDistance = Vector3.Distance(ringPose.position, palmPose.position);
        float littleDistance = Vector3.Distance(littlePose.position, palmPose.position);

        bool isFist = indexDistance < 0.07f && middleDistance < 0.07f && ringDistance < 0.07f && littleDistance < 0.07f;

        float distanceToObject = Vector3.Distance(palmPose.position, transform.position);
        bool handIsClose = distanceToObject < grabRange;

        if (isFist && !wasFist && handIsClose)
        {
            isGrabbing = !isGrabbing;
        }

        if (isGrabbing)
        {
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = palmPose.position + Vector3.up * (floatHeight + bob);
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
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

    }

    void OnDisable()
    {
        handSubsystem?.Stop();
    }
}
