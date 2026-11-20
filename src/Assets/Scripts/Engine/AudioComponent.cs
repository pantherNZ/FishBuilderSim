using System.Collections;
using Schema.Audio;
using UnityEngine;

namespace Runtime.Audio
{
    [DisallowMultipleComponent]
    public class AudioComponent : MonoBehaviour
    {
        public enum PlaybackType
        {
            World,
            UI,
        }

        public bool playOnEnable = true;
        public AudioDataSchema asset;
        public PlaybackType playbackType;
        public float delaySec;
        public bool dialogueShouldInterrupt;
        private Coroutine coroutine;

        public void Play()
        {
            coroutine = StartCoroutine(Coroutine());
        }

        private void OnEnable()
        {
            if (playOnEnable)
                Play();
        }

        private void OnDisable()
        {
            if (coroutine != null)
                StopCoroutine(coroutine);
            coroutine = null;
        }

        IEnumerator Coroutine()
        {
            if (asset == null)
                yield break;

            if (delaySec > 0.0f)
                yield return new WaitForSeconds(delaySec);

            if (!isActiveAndEnabled || asset == null || SfxManager.Instance == null)
                yield break;

            switch (playbackType)
            {
                case PlaybackType.UI:
                    SfxManager.Instance.PlayUI(asset);
                    break;

                default:
                    SfxManager.Instance.Play(asset, transform.position);
                    break;
            }
        }
    }
}