using UnityEngine;
using UnityEngine.SceneManagement;

namespace Khuthon
{
    public class PortalToMap : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private string targetSceneName = "MAP";
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private AudioClip transitionSFX;

        private bool _isTransitioning = false;

        private void OnTriggerEnter(Collider other)
        {
            if (_isTransitioning) return;
            // 플레이어 태그 확인 (부모까지 확인)
            if (other.CompareTag(playerTag) || (other.transform.parent != null && other.transform.parent.CompareTag(playerTag)))
            {
                StartCoroutine(TransitionRoutine());
            }
        }

        private System.Collections.IEnumerator TransitionRoutine()
        {
            _isTransitioning = true;
            Debug.Log($"[Portal] {targetSceneName} 씬으로 돌아갑니다.");

            if (transitionSFX != null)
            {
                // 소리가 끊기지 않게 PlayClipAtPoint 사용하거나 
                // 잠시 대기 후 이동
                AudioSource.PlayClipAtPoint(transitionSFX, Camera.main.transform.position);
                yield return new WaitForSeconds(0.3f); // 효과음이 살짝 들릴 시간을 줌
            }

            SceneManager.LoadScene(targetSceneName);
        }
    }
}
