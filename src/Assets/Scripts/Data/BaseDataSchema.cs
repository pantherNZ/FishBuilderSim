using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace Schema
{
	[Serializable]
	[JsonConverter(typeof(AssetJsonConverter))]
	public class BaseDataSchema : ScriptableObject, IDeserializationCallback
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

		public void OnDeserialization(object sender)
		{
			Init();
		}

		private void OnEnable()
		{
			Init();
		}

		public override int GetHashCode()
		{
			if (!hash.HasValue)
				CalculateHash();
			return hash.Value;
		}

		private void CalculateHash()
		{
			var l1 = Encoding.ASCII.GetByteCount(_id);
			var l2 = Encoding.ASCII.GetByteCount(_category);
			byte[] buff = new byte[l1 + l2];
			Encoding.ASCII.GetBytes(_id, 0, _id.Length, buff, 0);
			Encoding.ASCII.GetBytes(_category, 0, _category.Length, buff, _id.Length);
			hash = (int)xxHashSharp.xxHash.CalculateHash(buff, seed: 23246781);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
				return true;

			if (obj is not BaseDataSchema other)
				return false;

			if (GetType() != other.GetType())
				return false;

			return GetHashCode() == other.GetHashCode();
		}

		public event System.Action onDataChanged;

		// Called when the data manager loads this asset
		public virtual void OnDataLoaded() { }

		protected virtual void OnValidate()
		{
			onDataChanged?.Invoke();

			Init(_triggerHashRecalculate);
			_triggerHashRecalculate = false;
		}

		public void Init(bool forceUpdate = false)
		{
			if (forceUpdate || string.IsNullOrEmpty(id))
				_id = name;
			if (forceUpdate || string.IsNullOrEmpty(category))
			{
#if UNITY_EDITOR
				var path = Utility.GetResourcePath(this);
				if (!string.IsNullOrEmpty(path))
					_category = path.Split('/')[^2];
				else
					_category = string.Empty;
#endif

				CalculateHash();
			}
		}
	}

	public static partial class AssetBinaryConverter
	{
		public static void Write<T>(this BinaryWriter writer, T value) where T : BaseDataSchema
		{
			writer.Write(value?.GetHashCode() ?? 0);
		}

		public static T ReadDataSchema<T>(this BinaryReader reader) where T : BaseDataSchema
		{
			var hash = reader.ReadInt32();
			if (hash == 0)
				return null;
			return DataManager.Instance.FindAssetByHash(hash) as T;
		}
	}

	public class AssetJsonConverter : JsonConverter
	{
		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			BaseDataSchema asset = (BaseDataSchema)value;
			if (asset == null)
			{
				writer?.WriteValue(0);
			}
			else
			{
				var hash = asset.GetHashCode();
				writer?.WriteValue(hash);
			}
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.Value == null)
				return null;

			long hash = (long)reader.Value;
			if (hash == 0)
				return null;

			return DataManager.Instance.FindAssetByHash((int)hash);
		}

		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(BaseDataSchema);
		}
	}
}