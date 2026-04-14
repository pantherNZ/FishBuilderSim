
using System;
using System.Collections.Generic;
using Schema.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif

namespace Schema.Audio
{
	[Serializable]
	public class SfxEntry
	{
		public AudioClip clip;
		public UnityNullable<Interval> pitchRange;
		public UnityNullable<float> volumeOverride;
	}

	[Serializable]
	public enum PlayMethod
	{
		Sequence,
		Random,
		RandomSequence,
	}

	[CreateAssetMenu(fileName = "AudioDataSchema", menuName = "FishBuilderSim/Audio/AudioDataSchema")]
	public class AudioDataSchema : BaseDataSchema
	{
		public AudioDataSchema automaticChainTo;
		public float automaticChainToDelaySec;
		public float startDelaySec;
		public List<SfxEntry> entries;
		public PlayMethod playMethod = PlayMethod.Random;
		public UnityNullable<int> maxPerSecond;
		public bool looping;
		public UnityNullable<float> volumeOverride;
		public bool disabled;
		// If set, we don't generate a specific enum entry for this and instead we play it via the group
		public AudioDataSchema groupedWith;
		public float pitchMultiplier = 1.0f;

		protected override void OnValidate()
		{
			base.OnValidate();

			Runtime.Audio.SfxManager.ResetData(this);
		}
	}
}

#if UNITY_EDITOR
[CustomEditor( typeof( AudioDataSchema ), editorForChildClasses: true )]
public class AudioDataSchemaEditor : Editor
{
	Runtime.Audio.SfxManager.AudioInstance instance;
	static Button playBtn;
	static Button stopBtn;

	public override VisualElement CreateInspectorGUI()
	{
		var root = new VisualElement();
		InspectorElement.FillDefaultInspector( root, serializedObject, this );
		playBtn = new Button();
		playBtn.text = "Play";
		root.Add( playBtn );

		stopBtn = new Button();
		stopBtn.text = "Stop";
		stopBtn.SetEnabled( false );
		root.Add( stopBtn );

		playBtn.clicked += () =>
		{
			instance = Runtime.Audio.SfxManager.PlayInEditor( target as AudioDataSchema );
			EditorApplication.update += Update;
			playBtn.SetEnabled( false );
			stopBtn.SetEnabled( true );
		};

		stopBtn.clicked += () =>
		{
			Stop();
		};

		return root;
	}

	public void Update()
	{
		bool stopped = instance == null || instance.source == null || ( !instance.source.isPlaying && !instance.betweenChains );
		playBtn.SetEnabled( stopped );
		stopBtn.SetEnabled( !stopped );
		if ( stopped )
			Stop();
	}

	private void Stop()
	{
		Runtime.Audio.SfxManager.Stop( instance );
		EditorApplication.update -= Update;
		playBtn.SetEnabled( true );
		stopBtn.SetEnabled( false );
	}
}
#endif