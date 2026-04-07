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

    public bool IsExtruding => _isExtrudingLeft || _isExtrudingRight;

    void OnEnable()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
        {
            _handSubsystem = subsystems[0];
            _handSubsystem.Start();
        }

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

        if (_rb != null)
            _rb.isKinematic = _isExtrudingLeft || _isExtrudingRight || _rb.isKinematic;
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

        mesh.vertices = newVerts.ToArray();
        mesh.triangles = newTris.ToArray();
        mesh.RecalculateNormals();

        _newSphereIndices = new int[4];
        _newSphereStartPositions = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 worldPos = spheres[face.sphereIndices[i]].position;
            _newSphereStartPositions[i] = worldPos;

            GameObject marker = Instantiate(vertexMarkerPrefab, worldPos, Quaternion.identity);
            marker.transform.localScale = spheres[0].localScale;
            marker.layer = spheres[0].gameObject.layer;
            marker.name = $"Vertex_Extruded_{i}";
            marker.transform.SetParent(target);
            _newSphereIndices[i] = target.childCount - 1;
        }

        _deformer.RebuildVertexMap();
        _selector.BuildFaceList();

        Transform firstNew = _deformer.Spheres[_newSphereIndices[0]];
        _activeHUD = firstNew.GetComponent<VertexHUD>();
        if (_activeHUD != null) _activeHUD.Show();
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

        if (_activeHUD != null) _activeHUD.UpdateHUD();
    }

    private void FinishExtrude(bool isLeft)
    {
        if (isLeft) _isExtrudingLeft = false;
        else _isExtrudingRight = false;

        if (_activeHUD != null)
        {
            _activeHUD.Hide();
            _activeHUD = null;
        }

        _selector.BuildFaceList();

        _newSphereIndices = null;
        _newSphereStartPositions = null;
    }
}
