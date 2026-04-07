using UnityEngine;

/// Mapeia cada esfera aos vertices reais da mesh. A cada frame move os vertices
/// junto com as esferas. RebuildVertexMap() permite reconstruir apos extrude.
public class MeshDeformer : MonoBehaviour
{
    private Mesh mesh;
    private Vector3[] vertices;
    private Transform[] spheres;
    private int[][] vertexMap;

    public Mesh SharedMesh => mesh;
    public int[][] VertexMap => vertexMap;
    public Transform[] Spheres => spheres;


    void Start()
    {

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        mesh = meshFilter.mesh;

        vertices = mesh.vertices;

        spheres = new Transform[transform.childCount];
        vertexMap = new int[spheres.Length][];

        for (int i = 0; i < spheres.Length;  i++)
        {
            spheres[i] = transform.GetChild(i);
        }

        BuildVertexMap();

    }

    void Update()
    {
        for(int i = 0; i < spheres.Length; i++)
        {
            for (int j = 0; j < vertexMap[i].Length; j++)
            {
                vertices[vertexMap[i][j]] = transform.InverseTransformPoint(spheres[i].position);
            }
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals();

    }

    public void RebuildVertexMap()
    {
        vertices = mesh.vertices;

        spheres = new Transform[transform.childCount];
        for (int i = 0; i < spheres.Length; i++)
        {
            spheres[i] = transform.GetChild(i);
        }

        vertexMap = new int[spheres.Length][];
        BuildVertexMap();
    }

    void BuildVertexMap()
    {
        for(int i = 0; i <  spheres.Length; i++)
        {
              var matches = new System.Collections.Generic.List<int>();
              Vector3 localPos = transform.InverseTransformPoint(spheres[i].position);
            for (int j = 0; j < vertices.Length; j++)
            {
                
                if (Vector3.Distance(vertices[j], localPos) < 0.001f)
                {
                    matches.Add(j);
                }
            }
            vertexMap[i] = matches.ToArray();
        }

    }
}
