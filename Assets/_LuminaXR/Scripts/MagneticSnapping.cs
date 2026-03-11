using UnityEngine;


/// <summary>
/// Sistema de Atração Magnética de Vértices para VR.
/// Dispara uma esfera de colisão (ShepereCast) a partir do dedo do usuário
/// para capturar vértices próximos, compensando a falta de precisão motora.
/// </summary>
public class MagneticSnapping : MonoBehaviour
{
    [Header("Configurações de Raio de Atração")]
    public Transform indexFingerTip;
    public float snapRadius = 0.02f;
    public LayerMask vertexLayer;

    void Update()
    {
        if (indexFingerTip == null) return;

        Vector3 origin = indexFingerTip.position;
        Vector3 direction = indexFingerTip.forward;

        bool hitSucess = Physics.SphereCast(origin, snapRadius, direction, out RaycastHit hitInfo, 0.1f, vertexLayer);

        if (hitSucess)
        {
            Debug.DrawLine(origin, hitInfo.point, Color.green);

            Debug.Log("Vértice capturado magnéticamente: " + hitInfo.collider.name);

            // TODO: A lógica real de mover os vértices da malha entrará aqui depois.
        }else
        {
            Debug.DrawRay(origin, direction * 0.1f, Color.red);
        }

    }
   }


