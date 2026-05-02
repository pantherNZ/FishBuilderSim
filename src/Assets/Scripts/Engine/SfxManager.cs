using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Schema.Audio;
using UnityEditor;
using UnityEngine;

namespace Runtime.Audio
{
	[DisallowMultipleComponent]
	public class SfxManager : MonoEventReceiver
	{
		public class AudioInstance
		{
			public AudioDataSchema data;
			public AudioSource source;
			public Vector3? pos;
			public AudioClip clip;
			public bool automaticCleanup;
			public bool betweenChains;
			public bool fadingOut;
			public bool fadingIn;
			public event Action<AudioInstance> onFinished; // Called when audio finishes playing (will still be called if chaining)
			public event Action<AudioInstance> onCancelled; // Called when audio is stopped manually (not on finish)
			public event Action<AudioInstance> onChained; // Called after chaining to new sound

			public void Finished()
			{
				onFinished?.Invoke(this);
			}

			public void Chained()
			{
				onChained?.Invoke(this);
			}

			public void Cancelled()
			{
				onCancelled?.Invoke(this);
			}

			public bool IsAudioPlaying(bool ignoreFadingOut = true)
			{
				if (ignoreFadingOut)
				{
					return data != null &&
						source != null &&
						!fadingIn &&
						!fadingOut &&
						(source.isPlaying || betweenChains);
				}

				return data != null &&
					source != null &&
					!fadingIn &&
					(source.isPlaying || betweenChains);
			}
		}

		public class AudioType
		{
			public List<AudioInstance> instances = new();
			public float instancesPerSec;
			public List<SfxEntry> sequence = new();
		}

		// Singleton
		static SfxManager _instance;
		public static SfxManager Instance { get { return _instance; } }

		[SerializeField] AudioSource audioSourcePrefab;
		[SerializeField] float volumeScale = 1.0f;
		[SerializeField] float volumeScaleUI = 1.0f;
		[SerializeField] float volumeScaleSFX = 1.0f;
		[SerializeField] bool sfxEnabled;


		private List<GameObject> _audioPool;
		public bool SfxEnabled { get => sfxEnabled; set { sfxEnabled = value; } }
		static Dictionary<AudioDataSchema, AudioType> activeSfx = new();

		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				Debug.LogError("Multiple managers found!");
				Destroy(gameObject);
				return;
			}

			_instance = this;

			_audioPool = Memory.SimplePoolManager.Instance.GetPool("SFX");

			activeSfx.Clear();
		}

		private void FixedUpdate() => FixedUpdateStatic(_audioPool);

		private static void FixedUpdateStatic(List<GameObject> pool)
		{
			foreach (var sfxType in activeSfx.Values)
			{
				if (sfxType.instancesPerSec > 0.0f)
					sfxType.instancesPerSec -= Time.fixedDeltaTime;

				if (sfxType.instances.IsEmpty())
					continue;

				for (int i = sfxType.instances.Count - 1; i >= 0; --i)
				{
					var sfx = sfxType.instances[i];
					if (sfx.source == null)
					{
						Cleanup(pool, sfxType, i);
						continue;
					}

					if (!sfx.source.isPlaying && !sfx.fadingOut && !sfx.fadingIn && !sfx.betweenChains)
					{
						sfx.Finished();

						bool chained = false;
						if (sfx.data.automaticChainTo != null)
						{
							chained = HandleChaining(sfx);
						}

						if (!chained && sfx.automaticCleanup)
							Cleanup(pool, sfxType, i);
					}
				}
			}
		}

		private static bool HandleChaining(AudioInstance sfx, float additionalDelaySec = 0.0f)
		{
			var pos = sfx.pos;
			var volume = sfx.source.volume;
			var automaticCleanup = sfx.automaticCleanup;
			sfx.betweenChains = true;

			// Add 1ms to prevent the callback happening within this loop
			Utility.FunctionTimer.CreateTimer(0.01f + sfx.data.automaticChainToDelaySec + additionalDelaySec, () =>
			{
				sfx.betweenChains = false;

				if (_instance != null)
					_instance.PlayInternal(sfx.data.automaticChainTo, pos, volume, automaticCleanup, updateExisting: sfx);
#if UNITY_EDITOR
				else
					PlayInEditor(sfx.data.automaticChainTo, volume);
#endif
				sfx.Chained();

			});

			return true;
		}

		public static void ResetData(AudioDataSchema sfxType)
		{
			if (activeSfx.TryGetValue(sfxType, out var fx))
				fx.sequence.Clear();
		}

		public void Cleanup(AudioInstance instance)
		{
			if (instance == null)
				return;

			if (activeSfx.TryGetValue(instance.data, out var sfxType))
			{
				var foundIdx = sfxType.instances.IndexOf(instance);
				if (foundIdx != -1)
					Cleanup(_audioPool, sfxType, foundIdx);
			}
		}

		private static void Cleanup(List<GameObject> pool, AudioType sfxType, int idx)
		{
			if (idx < 0 || idx >= sfxType.instances.Count)
				return;

			if (sfxType.instances[idx].source != null)
			{
				sfxType.instances[idx].source.Stop();
				if (pool != null)
					Memory.SimplePoolManager.Instance?.Pool(sfxType.instances[idx].source.gameObject, pool);
				else
					DestroyImmediate(sfxType.instances[idx].source.gameObject);
			}

			sfxType.instances[idx].source = null;
			sfxType.instances.RemoveAt(idx);
		}

		public static void Stop(AudioInstance instance, float fadeOutTimeSec = 0.0f, bool playNextInChain = true)
		{
			if (instance != null && instance.source != null)
			{
				const float CancelledChainDelaySec = 1.0f;
				instance.Cancelled();

#if UNITY_EDITOR
				// Should be editor only
				if (Instance == null)
				{
					if (playNextInChain && instance.data.automaticChainTo != null)
						HandleChaining(instance, CancelledChainDelaySec);
					else
						Cleanup(null, activeSfx[instance.data], activeSfx[instance.data].instances.IndexOf(instance));
					return;
				}
#endif

				if (playNextInChain && instance.data.automaticChainTo != null)
				{
					Instance.StartCoroutine(FadeOut(instance, Mathf.Min(fadeOutTimeSec, CancelledChainDelaySec), null));
					HandleChaining(instance, CancelledChainDelaySec);
				}
				else
				{
					Instance.StartCoroutine(FadeOut(instance, fadeOutTimeSec, () =>
					{
						Cleanup(null, activeSfx[instance.data], activeSfx[instance.data].instances.IndexOf(instance));
					}));
				}
			}
		}

		public void Stop(Schema.AudioType sound, float fadeOutTimeSec = 0.0f)
		{
			if (sound == Schema.AudioType.None)
			{
				Debug.LogError("Tried to stop audio of type None");
				return;
			}
			Stop(sound.GetSchema(), fadeOutTimeSec);
		}

		public void Stop(AudioDataSchema sound, float fadeOutTimeSec = 0.0f)
		{
			if (activeSfx.TryGetValue(sound, out var audio))
			{
				foreach (var sfx in audio.instances)
				{
					sfx.Cancelled();
					Instance.StartCoroutine(FadeOut(sfx, fadeOutTimeSec, () =>
					{
						Memory.SimplePoolManager.Instance.Pool(sfx.source.gameObject, _audioPool);
					}));
				}

				audio.instances.Clear();
			}
		}

		public AudioInstance Play(AudioDataSchema sound, Vector3? pos, float volumeMultiplier = 1.0f, bool automaticCleanup = true)
			=> PlayInternal(sound, pos, GetVolume(volumeMultiplier) * /*Settings.SfxVolume **/ volumeScaleSFX, automaticCleanup);

		public AudioInstance Play(Schema.AudioType sound, Vector3? pos, float volumeMultiplier = 1.0f, bool automaticCleanup = true)
		{
			if (sound == Schema.AudioType.None)
			{
				Debug.LogError("Tried to play audio of type None");
				return null;
			}
			return Play(sound.GetSchema(), pos, volumeMultiplier, automaticCleanup);
		}

		private AudioInstance PlayInternal(AudioDataSchema sound, Vector3? pos, float volumeMultiplier = 1.0f, bool automaticCleanup = true, AudioInstance updateExisting = null)
		{
			if (!sfxEnabled || sound == null || sound.disabled)
				return null;

			return CreateAudio(sound, () =>
			{
				var audio = Memory.SimplePoolManager.Instance.Unpool(audioSourcePrefab.gameObject, _audioPool, pos ?? transform.position, Quaternion.identity);
				if (audio == null)
				{
					Debug.LogError("Failed to spawn audio from pool: " + sound.id + " at position " + pos);
					return null;
				}
				audio.GetComponent<AudioSource>().spatialBlend = pos != null ? 1.0f : 0.0f;
				return audio;
			},
				volumeMultiplier, automaticCleanup, checkMaxPerSecond: true, updateExisting, pos);
		}

		float GetVolume(float volumeMultiplier = 1.0f)
			=> /*Settings.MasterVolume*/ volumeScale * volumeMultiplier;

		public AudioInstance PlayUI(Schema.AudioType sound, float volumeMultiplier = 1.0f)
			=> PlayUI(sound.GetSchema(), volumeMultiplier);

		public AudioInstance PlayUI(AudioDataSchema sound, float volumeMultiplier = 1.0f)
			=> PlayInternal(sound, null, GetVolume(volumeMultiplier) * /*Settings.UIVolume */ volumeScaleUI);

		private static AudioInstance CreateAudio(AudioDataSchema sound, Func<GameObject> spawnFunc, float volumeMultiplier, bool automaticCleanup, bool checkMaxPerSecond,
			AudioInstance updateExisting = null, Vector3? pos = null)
		{
			var fxList = activeSfx.GetOrAdd(sound);

			if (checkMaxPerSecond && sound.maxPerSecond.HasValue && fxList.instancesPerSec >= sound.maxPerSecond.Value)
				return null;

			if (fxList.sequence.IsEmpty())
				fxList.sequence = sound.entries.Select(x => x).Reverse().ToList();

			var clip = sound.playMethod switch
			{
				PlayMethod.Sequence => fxList.sequence.PopBack(),
				PlayMethod.Random => fxList.sequence.RandomItem(),
				PlayMethod.RandomSequence => fxList.sequence.TakeRandomItem(),
				_ => fxList.sequence.RandomItem()
			};

			if (clip == null || clip.clip == null)
			{
				Debug.LogWarning($"Tried to play audio for {sound.id} but it had no entries");
				return null;
			}

			var newSfx = spawnFunc();
			var newSource = newSfx.GetOrAddComponent<AudioSource>();
			newSource.volume = volumeMultiplier * sound.volumeOverride.ValueOrDefault(1.0f) * clip.volumeOverride.ValueOrDefault(1.0f);
			newSource.pitch = sound.pitchMultiplier * (clip.pitchRange.HasValue ? clip.pitchRange.Value.Random(Utility.DefaultRng) : 1.0f);
			newSource.clip = clip.clip;
			newSource.loop = sound.looping;

			var newInstance = updateExisting ?? new AudioInstance();
			newInstance.data = sound;
			newInstance.source = newSource;
			newInstance.clip = clip.clip;
			newInstance.automaticCleanup = automaticCleanup;
			newInstance.fadingIn = sound.startDelaySec > 0.0f;
			newInstance.pos = pos;
			fxList.instancesPerSec++;

			Utility.FunctionTimer.CreateTimer(sound.startDelaySec, () =>
			{
				newSource.Play();
				newInstance.fadingIn = false;
			});

			if (updateExisting == null)
				fxList.instances.Add(newInstance);
			return newInstance;
		}

		private void StopAll()
		{
			foreach (var sfxType in activeSfx.Values)
			{
				foreach (var sfx in sfxType.instances)
				{
					sfx.source.Stop();
					Memory.SimplePoolManager.Instance.Pool(sfx.source.gameObject, _audioPool);
				}
			}

			activeSfx.Clear();
		}

		private static IEnumerator FadeOut(AudioInstance audio, float fadeOutTimeSec, Action onComplete)
		{
			audio.fadingOut = true;
			yield return audio.source.FadeOut(fadeOutTimeSec);
			audio.fadingOut = false;
			onComplete?.Invoke();
		}

#if UNITY_EDITOR
		public static AudioInstance PlayInEditor(AudioDataSchema sound, float volumeMultiplier = 1.0f)
		{
			EditorApplication.update += FixedUpdateEditor;
			return CreateAudio(sound, () => new GameObject(), volumeMultiplier, automaticCleanup: true, checkMaxPerSecond: false);
		}

		private static void FixedUpdateEditor()
		{
			FixedUpdateStatic(null);

			if (activeSfx.All(x => x.Value.instances.IsEmpty()))
			{
				EditorApplication.update -= FixedUpdateEditor;
			}
		}
#endif
	}
}
