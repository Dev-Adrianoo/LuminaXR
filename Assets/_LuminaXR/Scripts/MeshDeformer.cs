using UnityEditor.Rendering;
using UnityEngine;

public class MeshDeformer : MonoBehaviour
{

    private Mesh mesh;
    private Vector3[] vertices;
    private Transform[] spheres;
    private int[][] vertexMap;


    void Start()
    {

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        mesh = meshFilter.mesh;

        spheres = new Transform[transform.childCount];

        for (int i = 0; i < spheres.Length;  i++)
        {
            spheres[i] = transform.GetChild(i);
        } 

    }

    // Update is called once per frame
    void Update()
    {
        
    }

      void BuildVertexMap()
    {
        for(int i = 0; i <  spheres.Length; i++)
        {
              var matches = new System.Collections.Generic.List<int>();

            for (int j = 0; j < vertices.Length; j++)
            {
                Vector3 localPos = transform.InverseTransformPoint(spheres[i].position);


                if (Vector3.Distance(vertices[j], localPos) < 0.001f)
                {
                    matches.Add(j);
                }
            }
            vertexMap[i] = matches.ToArray();
        }

    }
}
