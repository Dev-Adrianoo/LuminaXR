using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

/// Executa extrude de face: duplica vertices, cria paredes laterais,
/// move na direcao da normal durante o arrasto.
public class FaceExtrude : MonoBehaviour
{
    [Header("Referencia")]
    public Transform target;
    public GameObject vertexMarkerPrefab;

    private XRHandSubsystem _handSubsystem;
    private MeshDeformer _deformer;
    private FaceSelector _selector;
    private Rigidbody _rb;

    private bool _isExtrudingLeft;
    private bool _isExtrudingRight;
    private Vector3 _extrudeNormal;
    private Vector3 _extrudeStartHand;
    private int[] _newSphereIndices;
    private Vector3[] _newSphereStartPositions;
    private VertexHUD _activeHUD;
    private bool _savedIsKinematic;
    private LineRenderer[] _extrudeLines;
    private static Material _dashMaterial;

    public bool IsExtruding => _isExtrudingLeft || _isExtrudingRight;

    public bool IsActiveForHand(bool isLeft) => _isExtrudingLeft || _isExtrudingRight;

    void OnEnable()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
            _handSubsystem = subsystems[0];

        if (target != null)
        {
            _deformer = target.GetComponent<MeshDeformer>();
            _rb = target.GetComponent<Rigidbody>();
        }

        _selector = FindAnyObjectByType<FaceSelector>();
    }

    void Update()
    {
        if (_handSubsystem == null || target == null || _deformer == null || _selector == null) return;

        ProcessHand(_handSubsystem.leftHand, true);
        ProcessHand(_handSubsystem.rightHand, false);

        if (_rb != null && IsExtruding)
            _rb.isKinematic = true;
    }

    private void ProcessHand(XRHand hand, bool isLeft)
    {
        bool isExtruding = isLeft ? _isExtrudingLeft : _isExtrudingRight;

        if (!hand.isTracked)
        {
            if (isExtruding) FinishExtrude(isLeft);
            return;
        }

        bool hasIndex = hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexPose);
        bool hasThumb = hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbPose);
        if (!hasIndex || !hasThumb)
        {
            if (isExtruding) FinishExtrude(isLeft);
            return;
        }

        float pinchDist = Vector3.Distance(indexPose.position, thumbPose.position);
        bool isPinching = pinchDist < 0.015f;
        Vector3 handPoint = (indexPose.position + thumbPose.position) * 0.5f;

        if (isExtruding)
        {
            if (!isPinching)
            {
                FinishExtrude(isLeft);
                return;
            }
            MoveExtrude(handPoint);
        }
        else
        {
            if (isPinching && _selector.IsActiveForHand(isLeft) && !_isExtrudingLeft && !_isExtrudingRight)
            {
                FaceData face = _selector.GetSelectedFace(isLeft);
                StartExtrude(face, handPoint, isLeft);
            }
        }
    }

    private void StartExtrude(FaceData face, Vector3 handPoint, bool isLeft)
    {
        if (isLeft) _isExtrudingLeft = true;
        else _isExtrudingRight = true;

        _extrudeNormal = face.normal;
        _extrudeStartHand = handPoint;

        if (_rb != null)
        {
            _savedIsKinematic = _rb.isKinematic;
            _rb.isKinematic = true;
        }

        Transform[] spheres = _deformer.Spheres;
        Mesh mesh = _deformer.SharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        int[][] vertexMap = _deformer.VertexMap;
        int[] faceMeshVerts = new int[4];
        for (int i = 0; i < 4; i++)
        {
            faceMeshVerts[i] = vertexMap[face.sphereIndices[i]][0];
        }

        int oldVertCount = vertices.Length;
        var newVerts = new List<Vector3>(vertices);
        int[] newMeshIndices = new int[4];
        for (int i = 0; i < 4; i++)
        {
            newMeshIndices[i] = oldVertCount + i;
            newVerts.Add(vertices[faceMeshVerts[i]]);
        }

        var newTris = new List<int>(triangles);
        foreach (int triStart in face.triangleStartIndices)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector3 triVertPos = vertices[newTris[triStart + i]];
                for (int j = 0; j < 4; j++)
                {
                    if (Vector3.Distance(triVertPos, vertices[faceMeshVerts[j]]) < 0.001f)
                    {
                        newTris[triStart + i] = newMeshIndices[j];
                        break;
                    }
                }
            }
        }

        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            int oldI = faceMeshVerts[i];
            int oldJ = faceMeshVerts[j];
            int newI = newMeshIndices[i];
            int newJ = newMeshIndices[j];

            newTris.Add(oldI);
            newTris.Add(oldJ);
            newTris.Add(newJ);

            newTris.Add(oldI);
            newTris.Add(newJ);
            newTris.Add(newI);
        }

        // Offset novos vertices por epsilon na normal para desambiguar do vertexMap
        Vector3 localNormal = target.InverseTransformDirection(_extrudeNormal).normalized;
        float epsilon = 0.002f;
        var vertsArray = newVerts.ToArray();
        for (int i = 0; i < 4; i++)
            vertsArray[newMeshIndices[i]] += localNormal * epsilon;

        mesh.vertices = vertsArray;
        mesh.triangles = newTris.ToArray();
        mesh.RecalculateNormals();

        // Posicao inicial das novas esferas derivada do vertice local (garante match com vertexMap)
        _newSphereIndices = new int[4];
        _newSphereStartPositions = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 worldPos = target.TransformPoint(vertsArray[newMeshIndices[i]]);
            _newSphereStartPositions[i] = worldPos;

            GameObject marker = Instantiate(vertexMarkerPrefab, worldPos, Quaternion.identity);
            marker.transform.SetParent(target);
            marker.transform.localScale = spheres[0].localScale;
            marker.layer = spheres[0].gameObject.layer;
            marker.name = $"Vertex_Extruded_{i}";
            Rigidbody markerRb = marker.GetComponent<Rigidbody>();
            if (markerRb != null) Destroy(markerRb);
            _newSphereIndices[i] = target.childCount - 1;
        }

        // DIAG: logar posicoes antes do rebuild para comparar
        for (int i = 0; i < 4; i++)
        {
            Vector3 vertLocal = vertsArray[newMeshIndices[i]];
            Transform newSphere = target.GetChild(_newSphereIndices[i]);
            Vector3 sphereLocal = target.InverseTransformPoint(newSphere.position);
            float dist = Vector3.Distance(vertLocal, sphereLocal);
            Debug.Log($"[Extrude DIAG] NewVert[{i}] local={vertLocal}, " +
                $"NewSphere[{i}] local={sphereLocal}, dist={dist:F6}, " +
                $"threshold=0.001, epsilon={epsilon}");
        }

        _deformer.RebuildVertexMap();

        // DIAG: verificar resultado do rebuild
        int[][] newMap = _deformer.VertexMap;
        for (int i = 0; i < 4; i++)
        {
            int sIdx = _newSphereIndices[i];
            Debug.Log($"[Extrude DIAG] After rebuild: sphere[{sIdx}] vertexMap.Length={newMap[sIdx].Length}");
        }

        Transform firstNew = _deformer.Spheres[_newSphereIndices[0]];
        _activeHUD = firstNew.GetComponent<VertexHUD>();
        if (_activeHUD != null) _activeHUD.Show();

        CreateExtrudeLines();
    }

    private void CreateExtrudeLines()
    {
        if (_dashMaterial == null)
        {
            // Textura pontilhada: 2px visivel + 2px transparente
            Texture2D tex = new Texture2D(4, 1, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.clear, Color.clear });
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;
            tex.Apply();

            Shader shader = Shader.Find("Sprites/Default");
            _dashMaterial = new Material(shader);
            _dashMaterial.mainTexture = tex;
            _dashMaterial.color = Color.cyan;
        }

        _extrudeLines = new LineRenderer[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject go = new GameObject($"ExtrudeLine_{i}");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.material = _dashMaterial;
            lr.startWidth = 0.002f;
            lr.endWidth = 0.002f;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.textureMode = LineTextureMode.Tile;
            lr.numCapVertices = 0;
            lr.startColor = Color.cyan;
            lr.endColor = Color.cyan;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, Vector3.zero);
            _extrudeLines[i] = lr;
        }
    }

    private void DestroyExtrudeLines()
    {
        if (_extrudeLines == null) return;
        for (int i = 0; i < 4; i++)
        {
            if (_extrudeLines[i] != null)
                Destroy(_extrudeLines[i].gameObject);
        }
        _extrudeLines = null;
    }

    private void MoveExtrude(Vector3 handPoint)
    {
        Vector3 handDelta = handPoint - _extrudeStartHand;
        float depth = Vector3.Dot(handDelta, _extrudeNormal);

        Transform[] spheres = _deformer.Spheres;
        for (int i = 0; i < 4; i++)
        {
            int sphereIdx = _newSphereIndices[i];
            if (sphereIdx < spheres.Length)
            {
                spheres[sphereIdx].position = _newSphereStartPositions[i] + _extrudeNormal * depth;
            }
        }

        // Feedback visual: linhas pontilhadas do ponto inicial ao atual
        if (_extrudeLines != null)
        {
            for (int i = 0; i < 4; i++)
            {
                int sphereIdx = _newSphereIndices[i];
                if (sphereIdx < spheres.Length && _extrudeLines[i] != null)
                {
                    _extrudeLines[i].SetPosition(0, _newSphereStartPositions[i]);
                    _extrudeLines[i].SetPosition(1, spheres[sphereIdx].position);
                }
            }
        }

        if (_activeHUD != null) _activeHUD.UpdateHUD();
    }

    private void FinishExtrude(bool isLeft)
    {
        if (isLeft) _isExtrudingLeft = false;
        else _isExtrudingRight = false;

        if (_rb != null)
            _rb.isKinematic = _savedIsKinematic;

        if (_activeHUD != null)
        {
            _activeHUD.Hide();
            _activeHUD = null;
        }

        DestroyExtrudeLines();

        _selector.BuildFaceList();

        _newSphereIndices = null;
        _newSphereStartPositions = null;
    }
}
