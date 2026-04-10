using UnityEngine;

public class DestroySelf : MonoBehaviour
{
    public UnityNullable<float> autoDestroyAfterSeconds;

    private void Start()
    {
        if ( autoDestroyAfterSeconds.HasValue )
            Destroy( gameObject, autoDestroyAfterSeconds.Value );
    }

    public void DestroyMe()
    {
        Destroy( gameObject );
    }
}