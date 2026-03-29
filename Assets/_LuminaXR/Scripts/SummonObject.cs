using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

/// <summary>
/// Palma aberta virada para cima atrai o objeto até a mão.
/// Se ambas as mãos estiverem abertas, prioriza a direita.
/// Feedback visual no objeto enquanto está sendo atraído.
/// </summary>
public class SummonObject : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _pullSpeed = 2f;
    [SerializeField] private float _arrivalDistance = 0.1f;
    [SerializeField] private float _palmUpThreshold = 0.7f;
    private SnapGravity _snapGravity;

    private XRHandSubsystem _handSubsystem;
    private MaterialPropertyBlock _mpb;
    private Renderer _renderer;
    private Vector3 _originalScale;

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private void OnEnable()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        _handSubsystem = subsystems.Count > 0 ? subsystems[0] : null;

        _mpb = new MaterialPropertyBlock();
        _renderer = _target.GetComponent<Renderer>();
        _originalScale = _target.localScale;
        _snapGravity = FindAnyObjectByType<SnapGravity>();
    }

    private void Update()
    {
        if (_handSubsystem == null || _target == null) return;
        if (_snapGravity != null && _snapGravity.IsSnapping) { StopFeedback(); return; }

        bool rightPalmUp = IsPalmUp(_handSubsystem.rightHand, out Pose rightPalm);
        bool leftPalmUp = IsPalmUp(_handSubsystem.leftHand, out Pose leftPalm);

        if (!rightPalmUp && !leftPalmUp)
        {
            StopFeedback();
            return;
        }

        // Prioriza direita se as duas estiverem abertas
        Pose targetPalm = rightPalmUp ? rightPalm : leftPalm;

        float distance = Vector3.Distance(_target.position, targetPalm.position);
        if (distance < _arrivalDistance)
        {
            StopFeedback();
            return;
        }

        _target.position = Vector3.Lerp(_target.position, targetPalm.position, _pullSpeed * Time.deltaTime);
        PlayFeedback();
    }

    private bool IsPalmUp(XRHand hand, out Pose palmPose)
    {
        palmPose = default;
        if (!hand.isTracked) return false;
        if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out palmPose)) return false;

        // Verifica se o vetor "up" da palma aponta para cima no mundo
        float dot = Vector3.Dot(-palmPose.up, Vector3.up);
        if (dot <= _palmUpThreshold) return false;

        // Bloqueia se polegar e médio estiverem próximos — postura de snap
        if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumb)) return false;
        if (!hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out Pose middleSnap)) return false;
        if (Vector3.Distance(thumb.position, middleSnap.position) < 0.04f) return false;

        // Verifica se os 4 dedos estão estendidos (longe da palma)
        if (!hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose index)) return false;
        if (!hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out Pose middle)) return false;
        if (!hand.GetJoint(XRHandJointID.RingTip).TryGetPose(out Pose ring)) return false;
        if (!hand.GetJoint(XRHandJointID.LittleTip).TryGetPose(out Pose little)) return false;

        float extendThreshold = 0.10f;
        return Vector3.Distance(index.position, palmPose.position) > extendThreshold &&
               Vector3.Distance(middle.position, palmPose.position) > extendThreshold &&
               Vector3.Distance(ring.position, palmPose.position) > extendThreshold &&
               Vector3.Distance(little.position, palmPose.position) > extendThreshold;
    }

    private void PlayFeedback()
    {
        // Pulsação de escala
        float pulse = 1f + Mathf.Sin(Time.time * 8f) * 0.04f;
        _target.localScale = _originalScale * pulse;

        // Tinge levemente de azul
        if (_renderer != null)
        {
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColor, Color.Lerp(Color.white, Color.cyan, 0.3f));
            _renderer.SetPropertyBlock(_mpb);
        }
    }

    private void StopFeedback()
    {
        if (_target == null) return;
        _target.localScale = _originalScale;

        if (_renderer != null)
        {
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColor, Color.white);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
