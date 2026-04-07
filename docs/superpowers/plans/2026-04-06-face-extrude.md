# Face Extrude Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow the user to pinch the center of a face and pull to extrude new geometry along the face normal.

**Architecture:** Two new scripts (FaceSelector + FaceExtrude) follow the existing pattern of separation of concerns. FaceSelector detects and highlights faces, FaceExtrude performs mesh operations. HandModeManager arbitrates conflicts via new HandMode.Extrude.

**Tech Stack:** Unity Mesh API, XRHandSubsystem, MaterialPropertyBlock

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `Assets/_LuminaXR/Scripts/FaceData.cs` | Struct for face representation |
| Create | `Assets/_LuminaXR/Scripts/FaceSelector.cs` | Face detection + highlight |
| Create | `Assets/_LuminaXR/Scripts/FaceExtrude.cs` | Mesh extrude operation |
| Modify | `Assets/_LuminaXR/Scripts/HandModeManager.cs` | Add Extrude enum + FaceSelector ref |
| Modify | `Assets/_LuminaXR/Scripts/MeshDeformer.cs` | Add RebuildVertexMap() |
| Modify | `Assets/_LuminaXR/Scripts/MagneticSnapping.cs` | Reduce radius + block during extrude |

---

### Task 1: FaceData struct + HandMode.Extrude + HandModeManager

**Files:**
- Create: `Assets/_LuminaXR/Scripts/FaceData.cs`
- Modify: `Assets/_LuminaXR/Scripts/HandModeManager.cs`

- [ ] **Step 1: Create FaceData.cs**

```csharp
using UnityEngine;

/// Representa uma face (quad) da mesh — 4 esferas, 2 triangulos, normal e centroide.
public struct FaceData
{
    public int[] sphereIndices;
    public int[] triangleStartIndices;
    public Vector3 normal;
    public Vector3 centroid;
}
```

- [ ] **Step 2: Add HandMode.Extrude to enum**

In `HandModeManager.cs` line 3, change:
```csharp
public enum HandMode { Neutral, Grab, Magnet, Modeling, Rotate }
```
To:
```csharp
public enum HandMode { Neutral, Grab, Magnet, Modeling, Rotate, Extrude }
```

- [ ] **Step 3: Add FaceSelector reference to HandModeManager**

Add field after line 26 (`private WristRotation _rotate;`):
```csharp
    private FaceSelector _extrude;
```

In `OnEnable()` after line 39, add:
```csharp
        _extrude = FindAnyObjectByType<FaceSelector>();
```

In `IsModeActiveForHand()` switch, add before the `_` default case:
```csharp
            HandMode.Extrude  => _extrude != null && _extrude.IsActiveForHand(isLeft),
```

- [ ] **Step 4: Update modePriority default array**

Change the default array to include Extrude above Modeling:
```csharp
    public HandMode[] modePriority = {
        HandMode.Extrude,
        HandMode.Modeling,
        HandMode.Rotate,
        HandMode.Grab,
        HandMode.Magnet,
        HandMode.Neutral
    };
```

- [ ] **Step 5: Commit**

```bash
git add Assets/_LuminaXR/Scripts/FaceData.cs Assets/_LuminaXR/Scripts/HandModeManager.cs
git commit -m "feat: add FaceData struct and HandMode.Extrude to HandModeManager"
```

---

### Task 2: MeshDeformer.RebuildVertexMap()

**Files:**
- Modify: `Assets/_LuminaXR/Scripts/MeshDeformer.cs`

- [ ] **Step 1: Make mesh and vertices accessible + add RebuildVertexMap()**

Replace the entire `MeshDeformer.cs` with:

```csharp
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

        for (int i = 0; i < spheres.Length; i++)
        {
            spheres[i] = transform.GetChild(i);
        }

        BuildVertexMap();
    }

    void Update()
    {
        for (int i = 0; i < spheres.Length; i++)
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
        for (int i = 0; i < spheres.Length; i++)
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
```

- [ ] **Step 2: Commit**

```bash
git add Assets/_LuminaXR/Scripts/MeshDeformer.cs
git commit -m "feat: add RebuildVertexMap and public accessors to MeshDeformer"
```

---

### Task 3: FaceSelector — core detection

**Files:**
- Create: `Assets/_LuminaXR/Scripts/FaceSelector.cs`

- [ ] **Step 1: Create FaceSelector.cs with face building and detection**

```csharp
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
            _deformer = target.GetComponent<MeshDeformer>();
    }

    void Start()
    {
        if (_deformer != null)
            BuildFaceList();
    }

    public void BuildFaceList()
    {
        _faces.Clear();
        Mesh mesh = _deformer.SharedMesh;
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;
        Transform[] spheres = _deformer.Spheres;

        // Agrupa triangulos por normal
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

        // Para cada grupo, encontra as 4 esferas e constroi FaceData
        foreach (var kv in groups)
        {
            List<int> triStarts = kv.Value;

            // Coleta posicoes unicas dos vertices do grupo
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

            // Mapeia posicoes para indices de esferas
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

            // Ordena esferas ao redor do perimetro por angulo
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

            // Cria pares (sphereIndex, angle) e ordena
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

        // Atualiza centroides e normais em runtime
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

            // Recalcula normal a partir das esferas
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

        // Verifica se HandModeManager permite (nao esta em outro modo ativo)
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

        // Distancia ao vertice mais proximo
        float closestVertexDist = float.MaxValue;
        for (int i = 0; i < spheres.Length; i++)
        {
            float d = Vector3.Distance(handPoint, spheres[i].position);
            if (d < closestVertexDist) closestVertexDist = d;
        }

        // Distancia ao centroide mais proximo
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

        // Face mode: centroide mais perto que qualquer vertice, dentro do range
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
```

- [ ] **Step 2: Commit**

```bash
git add Assets/_LuminaXR/Scripts/FaceSelector.cs
git commit -m "feat: add FaceSelector with face detection and highlight"
```

---

### Task 4: FaceExtrude — mesh topology + movement

**Files:**
- Create: `Assets/_LuminaXR/Scripts/FaceExtrude.cs`

- [ ] **Step 1: Create FaceExtrude.cs**

```csharp
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

        // Encontra os mesh vertex indices dos 4 vertices da face
        // Usa o primeiro vertexMap entry de cada esfera (todos mapeiam a mesma posicao)
        int[][] vertexMap = _deformer.VertexMap;
        int[] faceMeshVerts = new int[4];
        for (int i = 0; i < 4; i++)
        {
            faceMeshVerts[i] = vertexMap[face.sphereIndices[i]][0];
        }

        // Cria 4 novos vertices (copias das posicoes atuais)
        int oldVertCount = vertices.Length;
        var newVerts = new List<Vector3>(vertices);
        int[] newMeshIndices = new int[4];
        for (int i = 0; i < 4; i++)
        {
            newMeshIndices[i] = oldVertCount + i;
            newVerts.Add(vertices[faceMeshVerts[i]]);
        }

        // Reatribui triangulos da face original pros novos indices
        // Para cada vertice original da face, substitui pelo novo nos triangulos da face
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

        // Cria faces laterais (4 edges × 2 triangulos)
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            int oldI = faceMeshVerts[i];
            int oldJ = faceMeshVerts[j];
            int newI = newMeshIndices[i];
            int newJ = newMeshIndices[j];

            // Tri 1
            newTris.Add(oldI);
            newTris.Add(oldJ);
            newTris.Add(newJ);
            // Tri 2
            newTris.Add(oldI);
            newTris.Add(newJ);
            newTris.Add(newI);
        }

        // Aplica mesh
        mesh.vertices = newVerts.ToArray();
        mesh.triangles = newTris.ToArray();
        mesh.RecalculateNormals();

        // Spawna 4 novas esferas
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

        // Rebuild deformer com novas esferas
        _deformer.RebuildVertexMap();

        // Rebuild face list no selector
        _selector.BuildFaceList();

        // Ativa VertexHUD numa das novas esferas
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

        // Rebuild para estado final limpo
        _selector.BuildFaceList();

        _newSphereIndices = null;
        _newSphereStartPositions = null;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/_LuminaXR/Scripts/FaceExtrude.cs
git commit -m "feat: add FaceExtrude with mesh topology and movement"
```

---

### Task 5: MagneticSnapping adjustments

**Files:**
- Modify: `Assets/_LuminaXR/Scripts/MagneticSnapping.cs`

- [ ] **Step 1: Reduce magneticRadius**

In `MagneticSnapping.cs` line 18, change:
```csharp
    public float magneticRadius = 0.05f;
```
To:
```csharp
    public float magneticRadius = 0.025f;
```

- [ ] **Step 2: Block during Extrude mode**

In `MagneticSnapping.cs` line 115, change:
```csharp
                if (mode == HandMode.Rotate) return;
```
To:
```csharp
                if (mode == HandMode.Rotate || mode == HandMode.Extrude) return;
```

- [ ] **Step 3: Commit**

```bash
git add Assets/_LuminaXR/Scripts/MagneticSnapping.cs
git commit -m "feat: reduce magneticRadius and block snapping during extrude"
```

---

### Task 6: Scene setup

**Manual steps in Unity Editor:**

- [ ] **Step 1: Add FaceSelector component**

On the Camera Rig (same GameObject as HandModeManager):
- Add Component → FaceSelector
- Set `target` → TestVertex (o cubo)
- Set `detectionRange` → 0.15

- [ ] **Step 2: Add FaceExtrude component**

On the Camera Rig:
- Add Component → FaceExtrude
- Set `target` → TestVertex
- Set `vertexMarkerPrefab` → o mesmo prefab usado no VertexMarker

- [ ] **Step 3: Update HandModeManager priority array**

No Inspector do HandModeManager:
- Expandir `modePriority`
- Garantir ordem: Extrude, Modeling, Rotate, Grab, Magnet, Neutral

- [ ] **Step 4: Verificar magneticRadius**

No Inspector do MagneticSnapping:
- Confirmar que `magneticRadius` mostra 0.025
- Se o valor serializado antigo (0.05) ainda estiver, corrigir manualmente

- [ ] **Step 5: Build and test on Quest**

Testar sequencia:
1. Mao perto do centro de uma face → 4 esferas ficam amarelas
2. Mao perto de um vertice → esferas nao destacam (MagneticSnapping cuida)
3. Pinch no centro da face → geometria duplica, 4 novas esferas aparecem
4. Puxar na direcao da normal → face se move, VertexHUD mostra profundidade
5. Soltar → extrude completo, novas esferas editaveis individualmente
6. Repetir extrude na nova face → extrude em cima de extrude

- [ ] **Step 6: Commit scene changes**

```bash
git add Assets/_LuminaXR/Scenes/SampleScene.unity
git commit -m "feat: add FaceSelector and FaceExtrude to scene"
```

---

## Risk Notes

- **Normals invertidas nas paredes laterais:** se faces laterais ficarem invisiveis (backface culling), inverter a ordem dos vertices nos triangulos laterais — trocar `(oldI, oldJ, newJ)` por `(oldJ, oldI, newJ)` e ajustar o segundo triangulo.
- **vertexMarkerPrefab precisa ter VertexHUD:** se o prefab nao tiver o componente VertexHUD, o HUD durante extrude nao vai funcionar. Verificar o prefab antes de testar.
- **Valores serializados no Inspector:** o magneticRadius antigo (0.05) pode estar serializado na cena e sobrescrever o default do codigo. Verificar manualmente no Inspector.
