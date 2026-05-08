using UnityEngine;
using TMPro;

public class TMPFloatingAnimator : MonoBehaviour
{
    [Header("Color Changing Settings (Achromatic)")]
    [Tooltip("체크하면 두 색상 사이를 부드럽게 오갑니다.")]
    public bool enableColorChange = true;
    [Tooltip("색상이 변하는 속도 (낮을수록 천천히 변합니다)")]
    public float colorChangeSpeed = 0.3f;
    
    [Tooltip("첫 번째 색상 (예: 밝은 흰색)")]
    public Color color1 = Color.white;
    [Tooltip("두 번째 색상 (예: 어두운 회색)")]
    public Color color2 = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Header("Sparkle Settings")]
    public float sparkleSpeed = 2f; // 깜빡임도 살짝 여유롭게 조정
    [Range(0f, 1f)]
    public float minAlpha = 0.3f;
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

        // 1. 깜빡임 (투명도) 효과 계산
        float tAlpha = (Mathf.Sin(Time.time * sparkleSpeed) + 1f) / 2f; 
        float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, tAlpha);

        Color targetColor = _textMesh.color;

        // 2. 지정된 두 색상(무채색) 사이를 부드럽게 왕복하는 효과
        if (enableColorChange)
        {
            // 시간에 따라 0 ~ 1 사이를 왕복 (PingPong)
            float tColor = Mathf.PingPong(Time.time * colorChangeSpeed, 1f);
            targetColor = Color.Lerp(color1, color2, tColor);
        }

        // 3. 최종 색상에 투명도 적용
        targetColor.a = currentAlpha;
        _textMesh.color = targetColor;
    }
}
