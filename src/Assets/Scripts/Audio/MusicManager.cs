using System;
using System.Collections.Generic;
using DG.Tweening;
using Runtime.Events;
using Runtime.Game;
using UnityEngine;

namespace Runtime.Audio
{
	[Serializable]
	public class MusicEntry
	{
		public AudioClip clip;
		public UnityNullable<SceneReference> specificSceneRequirement;
		public UnityNullable<Interval> dangerRangeRequirement;
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
				if ( musicEnabled != value )
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
			if ( musicManager != null && musicManager != this )
			{
				Destroy( gameObject );
				return;
			}

			musicManager = this;
			DontDestroyOnLoad( this );
			rng = new Utility.UnityRandom();
		}

		private void Start()
		{
			Events.SettingsModified.Subscribe( this, UpdateSettings );
			Events.LorePlayed.Subscribe( this, LorePlayed );
			Events.DialogueOrLoreFinished.Subscribe( this, DialogueOrLoreFinished );
			Events.SceneChanging.Subscribe( this, SceneChanging );

			RestartMusic();
		}

		void UpdateSettings( Events.SettingsModified _e )
		{
			volumeScaleFromSettings = Settings.MusicVolume;
			UpdateAudioVolume();
		}

		void LorePlayed( Events.LorePlayed _e )
		{
			resumeMusicFade?.Kill();
			volumeScaleFromLorePLaying = 0.5f;
			UpdateAudioVolume();
		}

		void DialogueOrLoreFinished( Events.DialogueOrLoreFinished _e )
		{
			resumeMusicFade?.Kill();
			resumeMusicFade = DOTween.Sequence().Append( DOTween.To( () => volumeScaleFromLorePLaying, x =>
			{
				volumeScaleFromLorePLaying = x;
				UpdateAudioVolume();
			}, 1.0f, 1.0f ) );
		}

		void SceneChanging( Events.SceneChanging sceneChanging )
		{
			if ( sceneChanging.oldScene.sceneType == GameSceneManager.SceneType.MainMenu ||
				sceneChanging.newScene.sceneType == GameSceneManager.SceneType.MainMenu )
				RestartMusic();
		}

		public void StopMusic()
		{
			timer?.Stop();
			audioSource.Stop();
		}

		public void RestartMusic()
		{
			StopMusic();
			timer = Utility.FunctionTimer.CreateTimer( fadeInTimeSec, StartTrack );
		}

		private void StartTrack()
		{
			if ( !musicEnabled )
				return;

			var selector = new WeightedSelector<AudioClip>( rng );
			var localPlayer = Network.ClientSession.Instance?.LocalPlayerController;
			var danger = localPlayer?.difficultyScale.difficulty.value ?? 0;

			foreach ( var track in tracks )
			{
				if ( track.specificSceneRequirement.HasValue && GameSceneManager.Instance.CurrentScene != track.specificSceneRequirement.Value )
					continue;

				if ( track.dangerRangeRequirement.HasValue && !track.dangerRangeRequirement.Value.Contains( danger ) )
					continue;

				if ( track.clip == lastPlayed )
					continue;

				selector.AddItem( track.clip, 1 );
			}

			var sound = selector.GetResult();

			if ( sound == null )
			{
				Debug.LogWarning( "Failed to select a music track" );
				timer = Utility.FunctionTimer.CreateTimer( sound.length + timeBetweenMusicSec.Random( rng ), StartTrack );
				return;
			}

			lastPlayed = sound;
			UpdateAudioVolume();
			audioSource.PlayOneShot( sound );

			timer = Utility.FunctionTimer.CreateTimer( sound.length + timeBetweenMusicSec.Random( rng ), StartTrack );
		}

		void UpdateAudioVolume()
		{
			audioSource.volume = volumeScale * volumeScaleFromSettings * volumeScaleFromLorePLaying * Settings.MasterVolume;
		}
	}
}
