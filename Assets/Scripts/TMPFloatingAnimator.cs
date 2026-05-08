using UnityEngine;
using TMPro;

public class TMPFloatingAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("밝아지고 어두워지는 왕복 속도 (낮을수록 천천히)")]
    public float animationSpeed = 0.5f;

    [Header("Color Settings (Achromatic)")]
    public Color color1 = Color.white;
    public Color color2 = new Color(0.6f, 0.6f, 0.6f, 1f);

    [Header("Alpha Settings")]
    [Tooltip("가장 어두워졌을 때의 투명도 (너무 투명해지지 않게 0.7 정도로 설정)")]
    [Range(0f, 1f)]
    public float minAlpha = 0.7f;
    [Range(0f, 1f)]
    public float maxAlpha = 1.0f;

    private TMP_Text _textMesh;

    void Start()
    {
        _textMesh = GetComponent<TMP_Text>();
    }

    void Update()
    {
        if (_textMesh == null) return;

        // 0 ~ 1 사이를 부드럽게 왕복하는 단일 패턴 (불규칙함 제거)
        float t = Mathf.PingPong(Time.time * animationSpeed, 1f);

        // 색상과 투명도를 똑같은 타이밍에 맞춰서 부드럽게 변환
        Color targetColor = Color.Lerp(color1, color2, t);
        targetColor.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        
        _textMesh.color = targetColor;
    }
}
