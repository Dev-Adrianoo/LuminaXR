using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

// Detecta dois punhos simultâneos por 2s → alterna entre VR (fundo preto) e MR (passthrough)
public class MRToggle : MonoBehaviour
{
    [Header("Referências")]
    public OVRPassthroughLayer passthroughLayer;
    public Canvas leftCanvas;
    public Canvas rightCanvas;
    public Image leftFillImage;
    public Image rightFillImage;

    [Header("Configuração")]
    public float holdDuration = 2f;
    public float fistThreshold = 0.07f; // 7cm, mesmo threshold do ObjectGrab

    private XRHandSubsystem handSubsystem;
    private float timer = 0f;
    private bool isMRActive;

    void OnEnable()
    {
        var subsystems = new System.Collections.Generic.List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
            handSubsystem = subsystems[0];
    }

    void Start()
    {
        // lê o estado inicial do passthrough em vez de assumir
        isMRActive = passthroughLayer != null && passthroughLayer.enabled;
        SetFill(0f);
    }

    void Update()
    {
        if (handSubsystem == null || !handSubsystem.running) return;

        bool leftFist = IsFist(handSubsystem.leftHand);
        bool rightFist = IsFist(handSubsystem.rightHand);

        if (leftFist && rightFist)
        {
            timer += Time.deltaTime;
            float fill = Mathf.Clamp01(timer / holdDuration);

            SetFill(fill);
            UpdateCanvasPosition(handSubsystem.leftHand, leftCanvas);
            UpdateCanvasPosition(handSubsystem.rightHand, rightCanvas);

            if (timer >= holdDuration)
            {
                Toggle();
                timer = 0f;
                SetFill(0f);
            }
        }
        else
        {
            timer = 0f;
            SetFill(0f);
        }
    }

    private bool IsFist(XRHand hand)
    {
        if (!hand.isTracked) return false;

        bool hasPalm   = hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose);
        bool hasIndex  = hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexPose);
        bool hasMiddle = hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out Pose middlePose);
        bool hasRing   = hand.GetJoint(XRHandJointID.RingTip).TryGetPose(out Pose ringPose);
        bool hasLittle = hand.GetJoint(XRHandJointID.LittleTip).TryGetPose(out Pose littlePose);

        if (!hasPalm || !hasIndex || !hasMiddle || !hasRing || !hasLittle) return false;

        return Vector3.Distance(indexPose.position, palmPose.position) < fistThreshold &&
               Vector3.Distance(middlePose.position, palmPose.position) < fistThreshold &&
               Vector3.Distance(ringPose.position, palmPose.position) < fistThreshold &&
               Vector3.Distance(littlePose.position, palmPose.position) < fistThreshold;
    }

    private void UpdateCanvasPosition(XRHand hand, Canvas canvas)
    {
        if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose)) return;

        // posiciona acima da palma
        canvas.transform.position = palmPose.position + Vector3.up * 0.12f;
        // sempre enfrenta a câmera (billboard)
        canvas.transform.rotation = Camera.main.transform.rotation;
    }

    private void SetFill(float value)
    {
        bool show = value > 0f;
        // usa a referência direta do canvas — image.canvas retorna null quando inativo
        leftCanvas.gameObject.SetActive(show);
        rightCanvas.gameObject.SetActive(show);
        leftFillImage.fillAmount = value;
        rightFillImage.fillAmount = value;
    }

    private void Toggle()
    {
        isMRActive = !isMRActive;
        passthroughLayer.enabled = isMRActive;
    }
}
