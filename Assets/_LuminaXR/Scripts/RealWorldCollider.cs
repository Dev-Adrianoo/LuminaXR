using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Busca âncoras do ambiente real via Scene Understanding e cria colliders invisíveis em cada superfície detectada.
/// </summary>
public class RealWorldCollider : MonoBehaviour
{
    [SerializeField] private Transform _trackingSpace;

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

        if (rooms.Count == 0)
        {
            Debug.Log("[RealWorldCollider] Nenhum quarto encontrado. Solicitando scan do ambiente...");
            var scanned = await OVRScene.RequestSpaceSetup();
            if (!scanned) return;

            await OVRAnchor.FetchAnchorsAsync(rooms, new OVRAnchor.FetchOptions
            {
                SingleComponentType = typeof(OVRRoomLayout),
            });
        }

        Debug.Log($"[RealWorldCollider] Quartos encontrados: {rooms.Count}");

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
                var position = pose.ComputeWorldPosition(_trackingSpace);
                var rotation = pose.ComputeWorldRotation(_trackingSpace);

                var go = new GameObject(string.Join(", ", classifications));
                go.transform.SetPositionAndRotation(position ?? Vector3.zero, rotation ?? Quaternion.identity);
                var collider = go.AddComponent<BoxCollider>();
                if (child.TryGetComponent(out OVRBounded3D bounds) && bounds.IsEnabled)
                    collider.size = bounds.BoundingBox.size;
            }
        }
    }
}
