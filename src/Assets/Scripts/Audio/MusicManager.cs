using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace Runtime.Audio
{
	[Serializable]
	public class MusicEntry
	{
		public AudioClip clip;
	}

	[DisallowMultipleComponent]
	public class MusicManager : MonoEventReceiver
	{
		// Singleton
		static MusicManager musicManager;
		public static MusicManager Instance => musicManager;
		[SerializeField] float fadeInTimeSec = 5.0f;
		[SerializeField] Interval timeBetweenMusicSec;
		[SerializeField] List<MusicEntry> tracks;
		[SerializeField] AudioSource audioSource;
		[SerializeField] float volumeScale = 1.0f;
		[SerializeField] bool musicEnabled;
		public bool MusicEnabled
		{
			get => musicEnabled; set
			{
				if (musicEnabled != value)
				{
					musicEnabled = value;
					RestartMusic();
				}
			}
		}

		float volumeScaleFromSettings = 1.0f;
		float volumeScaleFromLorePLaying = 1.0f;

		Utility.FunctionTimer timer;
		AudioClip lastPlayed;
		Sequence resumeMusicFade;
		Utility.UnityRandom rng;

		private void Awake()
		{
			if (musicManager != null && musicManager != this)
			{
				Destroy(gameObject);
				return;
			}

			musicManager = this;
			DontDestroyOnLoad(this);
			rng = new Utility.UnityRandom();
		}

		private void Start()
		{
			RestartMusic();
		}

		// void UpdateSettings(Events.SettingsModified _e)
		// {
		// 	volumeScaleFromSettings = Settings.MusicVolume;
		// 	UpdateAudioVolume();
		// }

		public void StopMusic()
		{
			timer?.Stop();
			audioSource.Stop();
		}

		public void RestartMusic()
		{
			StopMusic();
			timer = Utility.FunctionTimer.CreateTimer(fadeInTimeSec, StartTrack);
		}

		private void StartTrack()
		{
			if (!musicEnabled)
				return;

			var selector = new WeightedSelector<AudioClip>(rng);

			foreach (var track in tracks)
			{
				if (track.clip == lastPlayed)
					continue;

				selector.AddItem(track.clip, 1);
			}

			var sound = selector.GetResult();

			if (sound == null)
			{
				Debug.LogWarning("Failed to select a music track");
				timer = Utility.FunctionTimer.CreateTimer(sound.length + timeBetweenMusicSec.Random(rng), StartTrack);
				return;
			}

			lastPlayed = sound;
			UpdateAudioVolume();
			audioSource.PlayOneShot(sound);

			timer = Utility.FunctionTimer.CreateTimer(sound.length + timeBetweenMusicSec.Random(rng), StartTrack);
		}

		void UpdateAudioVolume()
		{
			audioSource.volume = volumeScale * volumeScaleFromSettings * volumeScaleFromLorePLaying;// * Settings.MasterVolume;
		}
	}
}
