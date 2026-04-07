using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;

/// Detecta qual face da mesh esta mais proxima da mao.
/// Destaca as 4 esferas da face selecionada. Expoe IsActiveForHand e GetSelectedFace.
public class FaceSelector : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Header("Deteccao")]
    public float detectionRange = 0.15f;

    [Header("Referencia")]
    public Transform target;

    private XRHandSubsystem _handSubsystem;
    private MeshDeformer _deformer;
    private List<FaceData> _faces = new List<FaceData>();

    private FaceData _selectedFaceLeft;
    private FaceData _selectedFaceRight;
    private bool _hasSelectionLeft;
    private bool _hasSelectionRight;

    private int[] _highlightedSpheresLeft = new int[0];
    private int[] _highlightedSpheresRight = new int[0];

    public bool IsActiveForHand(bool isLeft)
    {
        return isLeft ? _hasSelectionLeft : _hasSelectionRight;
    }

    public FaceData GetSelectedFace(bool isLeft)
    {
        return isLeft ? _selectedFaceLeft : _selectedFaceRight;
    }

    public Vector3 GetFaceCentroid(bool isLeft)
    {
        return isLeft ? _selectedFaceLeft.centroid : _selectedFaceRight.centroid;
    }

    public List<FaceData> Faces => _faces;

    private bool _facesBuilt;

    void OnEnable()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.Count > 0)
            _handSubsystem = subsystems[0];

        if (target != null)
            _deformer = target.GetComponent<MeshDeformer>();
    }

    public void BuildFaceList()
    {
        _facesBuilt = true;
        _faces.Clear();
        Mesh mesh = _deformer.SharedMesh;
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        Transform[] spheres = _deformer.Spheres;

        var groups = new Dictionary<int, List<int>>();
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 a = verts[tris[t]];
            Vector3 b = verts[tris[t + 1]];
            Vector3 c = verts[tris[t + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

            int groupKey = -1;
            foreach (var kv in groups)
            {
                int existingTri = kv.Value[0];
                Vector3 ea = verts[tris[existingTri]];
                Vector3 eb = verts[tris[existingTri + 1]];
                Vector3 ec = verts[tris[existingTri + 2]];
                Vector3 existingNormal = Vector3.Cross(eb - ea, ec - ea).normalized;

                if (Vector3.Dot(normal, existingNormal) > 0.99f)
                {
                    groupKey = kv.Key;
                    break;
                }
            }

            if (groupKey == -1)
            {
                groupKey = groups.Count;
                groups[groupKey] = new List<int>();
            }
            groups[groupKey].Add(t);
        }

        foreach (var kv in groups)
        {
            List<int> triStarts = kv.Value;

            var uniquePositions = new List<Vector3>();
            foreach (int t in triStarts)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 pos = verts[tris[t + i]];
                    bool found = false;
                    foreach (Vector3 u in uniquePositions)
                    {
                        if (Vector3.Distance(pos, u) < 0.001f) { found = true; break; }
                    }
                    if (!found) uniquePositions.Add(pos);
                }
            }

            if (uniquePositions.Count != 4) continue;

            int[] sphereIndices = new int[4];
            bool allMapped = true;
            for (int i = 0; i < 4; i++)
            {
                sphereIndices[i] = -1;
                for (int s = 0; s < spheres.Length; s++)
                {
                    Vector3 sphereLocal = target.InverseTransformPoint(spheres[s].position);
                    if (Vector3.Distance(sphereLocal, uniquePositions[i]) < 0.001f)
                    {
                        sphereIndices[i] = s;
                        break;
                    }
                }
                if (sphereIndices[i] == -1) { allMapped = false; break; }
            }

            if (!allMapped) continue;

            Vector3 center = Vector3.zero;
            foreach (Vector3 p in uniquePositions) center += p;
            center /= 4f;

            Vector3 faceNormal = Vector3.zero;
            int ft = triStarts[0];
            faceNormal = Vector3.Cross(
                verts[tris[ft + 1]] - verts[tris[ft]],
                verts[tris[ft + 2]] - verts[tris[ft]]
            ).normalized;

            Vector3 refDir = (uniquePositions[0] - center).normalized;
            Vector3 crossDir = Vector3.Cross(faceNormal, refDir);

            var indexed = new List<(int si, float angle)>();
            for (int i = 0; i < 4; i++)
            {
                Vector3 v = uniquePositions[i] - center;
                float angle = Mathf.Atan2(Vector3.Dot(v, crossDir), Vector3.Dot(v, refDir));
                indexed.Add((sphereIndices[i], angle));
            }
            indexed.Sort((a, b) => a.angle.CompareTo(b.angle));

            int[] sortedSpheres = new int[4];
            for (int i = 0; i < 4; i++) sortedSpheres[i] = indexed[i].si;

            _faces.Add(new FaceData
            {
                sphereIndices = sortedSpheres,
                triangleStartIndices = triStarts.ToArray(),
                normal = faceNormal,
                centroid = target.TransformPoint(center)
            });
        }
    }

    void Update()
    {
        if (_handSubsystem == null || target == null || _deformer == null) return;

        if (!_facesBuilt && _deformer.SharedMesh != null)
        {
            BuildFaceList();
            _facesBuilt = true;
        }

        if (_faces.Count == 0) return;

        UpdateFaceCentroids();

        ProcessHand(_handSubsystem.leftHand, true);
        ProcessHand(_handSubsystem.rightHand, false);
    }

    private void UpdateFaceCentroids()
    {
        Transform[] spheres = _deformer.Spheres;
        for (int f = 0; f < _faces.Count; f++)
        {
            FaceData face = _faces[f];
            Vector3 center = Vector3.zero;
            for (int i = 0; i < 4; i++)
                center += spheres[face.sphereIndices[i]].position;
            center /= 4f;

            Vector3 a = spheres[face.sphereIndices[0]].position;
            Vector3 b = spheres[face.sphereIndices[1]].position;
            Vector3 c = spheres[face.sphereIndices[2]].position;
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

            face.centroid = center;
            face.normal = normal;
            _faces[f] = face;
        }
    }

    private void ProcessHand(XRHand hand, bool isLeft)
    {
        if (!hand.isTracked)
        {
            ClearSelection(isLeft);
            return;
        }

        bool hasIndex = hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexPose);
        bool hasThumb = hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbPose);
        if (!hasIndex || !hasThumb) { ClearSelection(isLeft); return; }

        if (HandModeManager.Instance != null)
        {
            HandMode mode = HandModeManager.Instance.GetMode(isLeft);
            if (mode != HandMode.Neutral && mode != HandMode.Extrude)
            {
                ClearSelection(isLeft);
                return;
            }
        }

        Vector3 handPoint = (indexPose.position + thumbPose.position) * 0.5f;
        Transform[] spheres = _deformer.Spheres;

        float closestVertexDist = float.MaxValue;
        for (int i = 0; i < spheres.Length; i++)
        {
            float d = Vector3.Distance(handPoint, spheres[i].position);
            if (d < closestVertexDist) closestVertexDist = d;
        }

        float closestFaceDist = float.MaxValue;
        int closestFaceIdx = -1;
        for (int f = 0; f < _faces.Count; f++)
        {
            float d = Vector3.Distance(handPoint, _faces[f].centroid);
            if (d < closestFaceDist)
            {
                closestFaceDist = d;
                closestFaceIdx = f;
            }
        }

        if (closestFaceIdx >= 0 && closestFaceDist < closestVertexDist && closestFaceDist < detectionRange)
        {
            SetSelection(isLeft, _faces[closestFaceIdx]);
        }
        else
        {
            ClearSelection(isLeft);
        }
    }

    private void SetSelection(bool isLeft, FaceData face)
    {
        int[] prevSpheres = isLeft ? _highlightedSpheresLeft : _highlightedSpheresRight;
        ClearHighlight(prevSpheres);

        if (isLeft)
        {
            _selectedFaceLeft = face;
            _hasSelectionLeft = true;
            _highlightedSpheresLeft = face.sphereIndices;
        }
        else
        {
            _selectedFaceRight = face;
            _hasSelectionRight = true;
            _highlightedSpheresRight = face.sphereIndices;
        }

        ApplyHighlight(face.sphereIndices, new Color(1f, 0.9f, 0.2f));
    }

    private void ClearSelection(bool isLeft)
    {
        if (isLeft)
        {
            ClearHighlight(_highlightedSpheresLeft);
            _hasSelectionLeft = false;
            _highlightedSpheresLeft = new int[0];
        }
        else
        {
            ClearHighlight(_highlightedSpheresRight);
            _hasSelectionRight = false;
            _highlightedSpheresRight = new int[0];
        }
    }

    private void ApplyHighlight(int[] sphereIndices, Color color)
    {
        Transform[] spheres = _deformer.Spheres;
        foreach (int idx in sphereIndices)
        {
            if (idx < 0 || idx >= spheres.Length) continue;
            Renderer rend = spheres[idx].GetComponent<Renderer>();
            if (rend == null) continue;
            var prop = new MaterialPropertyBlock();
            prop.SetColor(BaseColorId, color);
            rend.SetPropertyBlock(prop);
        }
    }

    private void ClearHighlight(int[] sphereIndices)
    {
        if (_deformer == null) return;
        Transform[] spheres = _deformer.Spheres;
        foreach (int idx in sphereIndices)
        {
            if (idx < 0 || idx >= spheres.Length) continue;
            Renderer rend = spheres[idx].GetComponent<Renderer>();
            if (rend != null) rend.SetPropertyBlock(null);
        }
    }

    public void RegisterFace(FaceData face)
    {
        _faces.Add(face);
    }
}
