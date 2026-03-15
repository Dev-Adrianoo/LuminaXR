using UnityEngine;

/// <summary>
/// Instancia esferas visuais nas 8 quinas do cubo em runtime.
/// Cada esfera tem collider e Layer VertexTarget — compatível com MagneticSnapping.
/// </summary>
public class VertexMarker : MonoBehaviour
{

    [Header("Configuração dos Marcadores")]
    [Tooltip("Prefab da esfera que vai aparecer em cada vértice")]
    public GameObject vertexMarkerPrefab;


    [Tooltip("Tamanho visual da esfera. Não afeta a zona de detecção do MagneticSnapping")]
    public float markerScale = 0.02f; // 2cm

    // As 8 quinas de um cubo unitário em coordenadas locais
    // Unity usa cubo de tamanho 1x1x1, então as quinas ficam em ±0.5 em cada eixo
    private readonly Vector3[] localCorners = new Vector3[]
    {
        new Vector3(-0.5f, -0.5f, -0.5f),
        new Vector3( 0.5f, -0.5f, -0.5f),
        new Vector3(-0.5f,  0.5f, -0.5f),
        new Vector3( 0.5f,  0.5f, -0.5f),
        new Vector3(-0.5f, -0.5f,  0.5f),
        new Vector3( 0.5f, -0.5f,  0.5f),
        new Vector3(-0.5f,  0.5f,  0.5f),
        new Vector3( 0.5f,  0.5f,  0.5f),
    };

    void Start()
    {
        if (vertexMarkerPrefab == null)
        {
            Debug.LogError("[VertexMarker] Prefab não atribuído! Arraste uma esfera no Inspector.");
            return;
        }

        SpawnMarkers();
    }

    // Esse Método cria uma esfera para cada vértice do cubo, posicionando-as corretamente no mundo
    private void SpawnMarkers()
    {
        foreach (Vector3 localPos in localCorners)
        {
            Vector3 worldPos = transform.TransformPoint(localPos);
            GameObject marker = Instantiate(vertexMarkerPrefab, worldPos, Quaternion.identity);
            marker.transform.localScale = Vector3.one * markerScale;
            marker.name = $"Vertex_{localPos}";
            marker.transform.SetParent(transform);
        }
    }
}
