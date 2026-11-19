using System.Collections;
using Runtime.Game;
using Schema.Audio;
using Schema.Story;
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
            Dialogue,
            DialogueWorldPosition,
        }

        public bool playOnEnable = true;
        public AudioDataSchema asset;
        public PlaybackType playbackType;
        public float delaySec;
        public bool dialogueShouldInterrupt;
        private Coroutine coroutine;

        public void Play()
        {
            coroutine = StartCoroutine( Coroutine() );
        }

        private void OnEnable()
        {
            if ( playOnEnable )
                Play();
        }

        private void OnDisable()
        {
            if ( coroutine != null )
                StopCoroutine( coroutine );
            coroutine = null;
        }

        IEnumerator Coroutine()
        {
            if ( asset == null )
                yield break;

            yield return new WaitUntil( () => SfxManager.Instance != null && GameSceneManager.Instance != null && !GameSceneManager.Instance.IsLoading );

            if ( delaySec > 0.0f )
                yield return new WaitForSeconds( delaySec );

            if ( !isActiveAndEnabled || asset == null || SfxManager.Instance == null )
                yield break;

            switch ( playbackType )
            {
                case PlaybackType.UI:
                    SfxManager.Instance.PlayUI( asset );
                    break;

                case PlaybackType.Dialogue:
                    Audio.DialogueManager.Instance.RequestDialogue( asset as DialogueSchema, interrupt: dialogueShouldInterrupt );
                    break;

                case PlaybackType.DialogueWorldPosition:
                    Audio.DialogueManager.Instance.RequestDialogue( asset as DialogueSchema, worldPosition: transform.position, interrupt: dialogueShouldInterrupt );
                    break;
                default:
                    SfxManager.Instance.Play( asset, transform.position );
                    break;
            }
        }
    }
}