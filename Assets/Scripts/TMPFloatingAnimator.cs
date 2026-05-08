using UnityEngine;
using TMPro;

public class TMPFloatingAnimator : MonoBehaviour
{
    [Header("Sparkle Settings")]
    public float sparkleSpeed = 3f;
    [Range(0f, 1f)]
    public float minAlpha = 0.3f;
    [Range(0f, 1f)]
    public float maxAlpha = 1.0f;

    private TMP_Text _textMesh;

    void Start()
    {
        // 3D TextMeshPro와 UI TextMeshProUGUI 모두 지원하도록 TMP_Text 사용
        _textMesh = GetComponent<TMP_Text>();
    }

    void Update()
    {
        if (_textMesh == null) return;

        // 시간에 따라 투명도(Alpha)를 조절하여 제자리에서 깜빡깜빡 반짝이는 효과
        float t = (Mathf.Sin(Time.time * sparkleSpeed) + 1f) / 2f; // 0 ~ 1 사이의 값으로 변환
        float currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, t);
        
        Color currentColor = _textMesh.color;
        currentColor.a = currentAlpha;
        _textMesh.color = currentColor;
    }
}
