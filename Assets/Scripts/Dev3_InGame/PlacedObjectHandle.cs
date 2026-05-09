using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

namespace Khuthon.InGame
{
    /// <summary>
    /// 배치된 오브젝트의 메타데이터(Firebase 키, 이름, 설명, BGM 등)를 관리하고
    /// 추천 수에 따른 크기 조정 및 전용 BGM 재생을 담당합니다.
    /// </summary>
    public class PlacedObjectHandle : MonoBehaviour
    {
        [Header("데이터 설정 (인스펙터에서 수정 가능)")]
        [SerializeField] private string objectName = "새 오브젝트";
        [SerializeField, TextArea(3, 5)] private string description = "이 오브젝트에 대한 설명입니다.";
        [SerializeField] private string bgmPath;
        
        [Tooltip("오브젝트가 속한 연도를 선택하세요")]
        [Range(2000, 2026)]
        public int period = 2024; 

        [Header("시스템 정보 (자동 설정됨)")]
        [SerializeField] private string modelUrl;
        [SerializeField] private string userId = "player_1";
        [SerializeField] private string firebaseKey;

        // 속성 프로퍼티 (기존 코드와의 호환성 유지)
        public string ObjectName { get => objectName; set => objectName = value; }
        public string Description { get => description; set => description = value; }
        public string BgmPath { get => bgmPath; set => bgmPath = value; }
        public string Period { get => period.ToString(); set => int.TryParse(value, out period); }
        public string ModelUrl { get => modelUrl; set => modelUrl = value; }
        public string UserId { get => userId; set => userId = value; }
        public string FirebaseKey { get => firebaseKey; set => firebaseKey = value; }

        private AudioSource _audioSource;
        private AudioClip _cachedClip;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.spatialBlend = 1.0f; 
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = 1f;
            _audioSource.maxDistance = 15f; 
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
        }

        public void UpdateScale(int count)
        {
            float scaleFactor = 1.0f + (count * 0.1f);
            transform.localScale = Vector3.one * scaleFactor;
        }

        public void PlayBGM()
        {
            if (string.IsNullOrEmpty(bgmPath)) return;

            if (_cachedClip != null)
            {
                _audioSource.clip = _cachedClip;
                _audioSource.Play();
            }
            else
            {
                StartCoroutine(LoadAndPlayAudio());
            }
        }

        public void StopBGM()
        {
            if (_audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }

        private IEnumerator LoadAndPlayAudio()
        {
            string uri = "file://" + bgmPath.Replace("\\", "/");
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.MPEG))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    _cachedClip = DownloadHandlerAudioClip.GetContent(www);
                    _audioSource.clip = _cachedClip;
                    _audioSource.Play();
                }
            }
        }
    }
}
