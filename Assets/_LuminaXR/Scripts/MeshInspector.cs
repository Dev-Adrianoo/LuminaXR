using UnityEngine;

public class MeshInspector : MonoBehaviour
{
    void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null)
        {
            Debug.Log("[MeshFilter] Nenhum MeshFilter encontrado neste objeto");
            return;
        }

        Mesh mesh = meshFilter.mesh;

        Debug.Log($"[MeshInspector] Nome da malha: {mesh.name}");
        Debug.Log($"[MeshInspector] Total de vértices: {mesh.vertexCount}");
        Debug.Log($"[MeshInspector] Total de triângulos: {mesh.triangles.Length / 3}");


        for (int i = 0; i < mesh.vertices.Length; i++)
        {
            Debug.Log($"[MeshInspector] Vértice [{i}]: {mesh.vertices[i]}");
        }
    }
}
