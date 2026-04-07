using UnityEngine;

/// Representa uma face (quad) da mesh — 4 esferas, 2 triangulos, normal e centroide.
public struct FaceData
{
    public int[] sphereIndices;
    public int[] triangleStartIndices;
    public Vector3 normal;
    public Vector3 centroid;
}
