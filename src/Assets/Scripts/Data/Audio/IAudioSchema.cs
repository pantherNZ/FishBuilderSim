using System;
using UnityEngine;

namespace Schema.Audio
{
	public interface IAudioSchema
	{
		public abstract Runtime.Audio.SfxManager.AudioInstance Play( Vector3? pos );
	}
}
