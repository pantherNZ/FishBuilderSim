using System.Collections.Generic;
using UnityEngine;

public static partial class Utility
{
    /// <summary>
    /// Sets the layer of the given game object and all of its children recursively.
    /// </summary>
    /// <param name="obj">The game object to set the layer on</param>
    /// <param name="newLayer">The new layer to set</param>
    public static void SetLayerRecursively( GameObject obj, int newLayer )
    {
        if ( null == obj )
        {
            return;
        }

        obj.layer = newLayer;

        foreach ( Transform child in obj.transform )
        {
            if ( null == child )
            {
                continue;
            }
            SetLayerRecursively( child.gameObject, newLayer );
        }
    }

    public static bool TryGetComponentInChildren<T>( this GameObject gameObject, out T component )
    {
        component = gameObject.GetComponentInChildren<T>();
        return component != null;
    }

    public static bool TryGetComponentInChildren<T>( this GameObject gameObject, bool includeInactive, out T component )
    {
        component = gameObject.GetComponentInChildren<T>( includeInactive );
        return component != null;
    }

    public static bool TryGetComponentInChildren<T>( this MonoBehaviour comp, out T component )
          => comp.gameObject.TryGetComponentInChildren<T>( out component );
    public static bool TryGetComponentInChildren<T>( this MonoBehaviour comp, bool includeInactive, out T component )
        => comp.gameObject.TryGetComponentInChildren<T>( includeInactive, out component );

    // Stable Transform path from root -> child (e.g., "Root/Arm/Hand")
    public static string GetRelativePath( Transform child, Transform root )
    {
        if ( child == null )
            return string.Empty;
        var stack = new Stack<string>();
        var cur = child;
        while ( cur != null && cur != root )
        {
            stack.Push( cur.name );
            cur = cur.parent;
        }
        return string.Join( "/", stack );
    }
}