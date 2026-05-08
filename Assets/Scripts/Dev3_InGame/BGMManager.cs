using UnityEngine;

namespace Khuthon
{
    public class BGMManager : MonoBehaviour
    {
        private static BGMManager _instance;
        public static BGMManager Instance => _instance;

        [Header("오디오 설정")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip defaultBGM;
        [SerializeField] private bool playOnAwake = true;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }

                audioSource.loop = true;
                audioSource.playOnAwake = false;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (playOnAwake && defaultBGM != null)
            {
                PlayBGM(defaultBGM);
            }
        }

        public void PlayBGM(AudioClip clip)
        {
            if (audioSource.clip == clip) return;

            audioSource.clip = clip;
            audioSource.Play();
            Debug.Log($"[BGM] 재생 시작: {clip.name}");
        }

        public void StopBGM()
        {
            audioSource.Stop();
        }

        public void SetVolume(float volume)
        {
            audioSource.volume = Mathf.Clamp01(volume);
        }
    }
}
