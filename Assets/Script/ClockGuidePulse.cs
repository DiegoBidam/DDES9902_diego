using UnityEngine;

public class ClockGuidePulse : MonoBehaviour
{
    public float floatHeight = 0.06f;
    public float floatSpeed = 2f;
    public float pulseSpeed = 3f;

    private Renderer markerRenderer;
    private Material markerMaterial;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Color originalColor;
    private float animationTime;

    void Awake()
    {
        markerRenderer = GetComponent<Renderer>();
        originalPosition = transform.localPosition;
        originalScale = transform.localScale;

        if (markerRenderer == null)
        {
            Debug.LogError("ClockGuidePulse는 표시용 Sphere에 붙여주세요.");
            enabled = false;
            return;
        }

        markerMaterial = markerRenderer.material;
        originalColor = markerMaterial.GetColor("_BaseColor");
    }

    void Update()
    {
        if (markerRenderer == null || !markerRenderer.enabled)
            return;

        animationTime += Time.deltaTime;

        float pulse = (Mathf.Sin(animationTime * pulseSpeed) + 1f) * 0.5f;

        transform.localPosition = originalPosition
            + Vector3.up
            * Mathf.Sin(animationTime * floatSpeed)
            * floatHeight;

        transform.localScale = originalScale
            * Mathf.Lerp(0.85f, 1.15f, pulse);

        Color color = originalColor * Mathf.Lerp(0.35f, 1f, pulse);
        color.a = originalColor.a;
        markerMaterial.SetColor("_BaseColor", color);
    }

    void OnDestroy()
    {
        if (markerMaterial != null)
            Destroy(markerMaterial);
    }
}