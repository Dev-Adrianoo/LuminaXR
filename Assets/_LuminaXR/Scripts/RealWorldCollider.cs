using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Busca âncoras do ambiente real via Scene Understanding e cria colliders invisíveis em cada superfície detectada.
/// </summary>
public class RealWorldCollider : MonoBehaviour
{
    private void Start()
    {
        LoadSceneAsync();
    }

    private async void LoadSceneAsync()
    {
        if (!OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.Scene))
        {
            Debug.LogError("[RealWorldCollider] Permissão de Scene não concedida.");
            return;
        }

        var rooms = new List<OVRAnchor>();
        await OVRAnchor.FetchAnchorsAsync(rooms, new OVRAnchor.FetchOptions
        {
            SingleComponentType = typeof(OVRRoomLayout),
        });

        foreach (var room in rooms)
        {
            if (!room.TryGetComponent(out OVRAnchorContainer container)) continue;

            var children = new List<OVRAnchor>();
            await container.FetchAnchorsAsync(children);

            foreach (var child in children)
            {
                if (!child.TryGetComponent(out OVRSemanticLabels labels)) continue;

                var classifications = new HashSet<OVRSemanticLabels.Classification>();
                labels.GetClassifications(classifications);
                Debug.Log($"[RealWorldCollider] Encontrado: {string.Join(", ", classifications)}");

                if (!child.TryGetComponent(out OVRLocatable locatable)) continue;
                await locatable.SetEnabledAsync(true);

                if (!locatable.TryGetSceneAnchorPose(out var pose)) continue;
                var position = pose.ComputeWorldPosition(Camera.main.transform);
                var rotation = pose.ComputeWorldRotation(Camera.main.transform);

                var go = new GameObject(string.Join(", ", classifications));
                go.transform.SetPositionAndRotation(position ?? Vector3.zero, rotation ?? Quaternion.identity);
                go.AddComponent<BoxCollider>();
            }
        }
    }
}
