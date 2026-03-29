using UnityEngine;

/// <summary>
/// Posiciona o objeto na frente da câmera ao iniciar a cena.
/// </summary>
public class SpawnInFront : MonoBehaviour
{
    [SerializeField] private float _distance = 0.5f;

    private void Start()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var forward = cam.transform.forward;
        forward.y = 0f;
        forward.Normalize();
        transform.position = cam.transform.position + forward * _distance;
    }
}
