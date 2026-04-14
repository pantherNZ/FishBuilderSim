using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace Schema
{
	[Serializable]
	[JsonConverter( typeof( AssetJsonConverter ) )]
	public class BaseDataSchema : ScriptableObject
	{
		public string id
		{
			get => _id; set
			{
				_id = value;
				CalculateHash();
			}
		}
		public string category
		{
			get => _category; set
			{
				_category = value;
				CalculateHash();
			}
		}
		[SerializeField] string _id = string.Empty;
		[SerializeField] string _category = string.Empty;
		[SerializeField][ReadOnly] UnityNullable<int> hash;
		[SerializeField] bool _triggerHashRecalculate;

		// Debug/editor only used for hash collision checking
		[HideInInspector] public string path;

		private void OnEnable()
		{
			Init();
		}

		public override int GetHashCode()
		{
			if ( !hash.HasValue )
				CalculateHash();
			return hash.Value;
		}

		private void CalculateHash()
		{
			var l1 = Encoding.ASCII.GetByteCount( _id );
			var l2 = Encoding.ASCII.GetByteCount( _category );
			byte[] buff = new byte[l1 + l2];
			Encoding.ASCII.GetBytes( _id, 0, _id.Length, buff, 0 );
			Encoding.ASCII.GetBytes( _category, 0, _category.Length, buff, _id.Length );
			hash = ( int )xxHashSharp.xxHash.CalculateHash( buff, seed: 23246781 );
		}

		public override bool Equals( object obj )
		{
			if ( obj is not BaseDataSchema other )
				return false;
			return GetHashCode() == other.GetHashCode();
		}

		public event Action onDataChanged;

		// Called when the data manager loads this asset
		public virtual void OnDataLoaded() { }

		protected virtual void OnValidate()
		{
			onDataChanged?.Invoke();

			Init( _triggerHashRecalculate );
			_triggerHashRecalculate = false;
		}

		public void Init( bool forceUpdate = false )
		{
			if ( forceUpdate || string.IsNullOrEmpty( id ) )
				_id = name;
			if ( forceUpdate || string.IsNullOrEmpty( category ) )
			{
#if UNITY_EDITOR
				var path = Utility.GetResourcePath( this );
				if ( !string.IsNullOrEmpty( path ) )
					_category = path.Split( '/' )[^2];
				else
					_category = string.Empty;
#endif

				CalculateHash();
			}
		}
	}
}
