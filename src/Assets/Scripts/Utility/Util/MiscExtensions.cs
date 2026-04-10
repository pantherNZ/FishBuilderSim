using UnityEngine;

public static partial class Utility
{
	public static bool HasComponent<T>( this GameObject obj ) where T : Component
	{
		return obj.GetComponent<T>() != null;
	}

	public static Color? TryGetColour( this Material mat, string col )
		=> mat.HasColor( col ) ? mat.GetColor( col ) : null;

	public static GraphicsBufferHandle? TryGetBuffer( this Material mat, string col )
		=> mat.HasBuffer( col ) ? mat.GetBuffer( col ) : null;

	public static float? TryGetFloat( this Material mat, string col )
		=> mat.HasFloat( col ) ? mat.GetFloat( col ) : null;

	public static Texture TryGetTexture( this Material mat, string col )
		=> mat.HasTexture( col ) ? mat.GetTexture( col ) : null;

	public static int? TryGetInt( this Material mat, string col )
		=> mat.HasInteger( col ) ? mat.GetInteger( col ) : null;

	public static Matrix4x4? TryGetMatrix( this Material mat, string col )
		=> mat.HasMatrix( col ) ? mat.GetMatrix( col ) : null;

	public static int LayerValue( this LayerMask mask )
		=> Mathf.RoundToInt( Mathf.Log( mask.value, 2 ) );
}
