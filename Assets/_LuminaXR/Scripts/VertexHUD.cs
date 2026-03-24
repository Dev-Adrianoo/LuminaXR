using UnityEngine;
using TMPro;

/// <summary>
/// Exibe um texto flutuante acima da esfera do vértice durante o arrasto,
/// mostrando a distância percorrida em metros. Criado automaticamente
/// como filho da esfera — cada vértice tem seu próprio HUD.
/// </summary>
public class VertexHUD : MonoBehaviour
{
    private TextMeshPro tmp;
    private Vector3 startPosition;
    private bool isVisible;

    void Awake()
    {
        // Cria o TextMeshPro como filho da esfera
        // Compensa a escala do pai pra que o texto fique em tamanho real
        GameObject hudObj = new GameObject("HUD_Text");
        hudObj.transform.SetParent(transform);
        hudObj.transform.localPosition = Vector3.up * 8f;
        float parentScale = transform.lossyScale.x;
        if (parentScale > 0f)
        {
            hudObj.transform.localScale = Vector3.one * (1f / parentScale);
        }

        tmp = hudObj.AddComponent<TextMeshPro>();
        tmp.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (tmp.font == null)
        {
            Debug.LogError("[VertexHUD] Font não encontrada! Verifique se TMP Essentials foi importado.");
        }
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(2f, 1f);
        tmp.text = "---";

        Hide();
        Debug.Log("[VertexHUD] HUD criado em " + gameObject.name);
    }

    /// <summary>
    /// Chamado pelo MagneticSnapping quando o arrasto começa.
    /// Salva a posição inicial pra calcular a distância.
    /// </summary>
    public void Show()
    {
        startPosition = transform.position;
        isVisible = true;
        tmp.gameObject.SetActive(true);
        Debug.Log("[VertexHUD] Show() chamado em " + gameObject.name);
    }

    /// <summary>
    /// Chamado pelo MagneticSnapping a cada frame durante o arrasto.
    /// Atualiza o texto com a distância e faz o HUD olhar pra câmera.
    /// </summary>
    public void UpdateHUD()
    {
        if (!isVisible) return;

        float distance = Vector3.Distance(startPosition, transform.position);
        tmp.text = distance.ToString("F3") + "m";

        // Faz o texto sempre olhar pra câmera (Billboard)
        Camera cam = Camera.main;
        if (cam != null)
        {
            tmp.transform.rotation = Quaternion.LookRotation(tmp.transform.position - cam.transform.position);
        }
    }

    /// <summary>
    /// Chamado pelo MagneticSnapping quando solta o pinch.
    /// </summary>
    public void Hide()
    {
        isVisible = false;
        if (tmp != null)
        {
            tmp.gameObject.SetActive(false);
        }
    }
}
