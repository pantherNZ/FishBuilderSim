using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Memory
{
    [DisallowMultipleComponent]
    public class SimplePoolManager : MonoBehaviour
    {
        public static SimplePoolManager Instance;

        Dictionary<string, List<GameObject>> namedPools = new();

        void Awake()
        {
            Instance = this;
        }

        public List<GameObject> GetPool( string poolName )
        {
            if ( !namedPools.ContainsKey( poolName ) )
                namedPools[poolName] = new();
            return namedPools[poolName];
        }

        public void Pool( GameObject obj, List<GameObject> pool, float delay = 0f )
        {
            if ( delay <= 0f )
            {
                _Pool( obj, pool );
                return;
            }
            StartCoroutine( PoolCoroutine( obj, pool, delay ) );
        }

        IEnumerator PoolCoroutine( GameObject obj, List<GameObject> pool, float delay = 0f )
        {
            yield return new WaitForSeconds( delay );
            _Pool( obj, pool );
        }

        void _Pool( GameObject obj, List<GameObject> pool )
        {
            if ( obj == null )
                return;
            if ( pool == null )
            {
                Destroy( obj );
                return;
            }
            obj.SetActive( false );
            pool.Add( obj );
        }

        bool TryPopValidInstance( List<GameObject> pool, out GameObject instance )
        {
            instance = null;
            if ( pool == null )
                return false;

            while ( pool.Count > 0 )
            {
                var candidate = pool[^1];
                pool.RemoveAt( pool.Count - 1 );
                if ( candidate == null )
                    continue;

                instance = candidate;
                return true;
            }

            return false;
        }

        public GameObject Unpool( GameObject prefab, List<GameObject> pool, Vector3 position, Quaternion rotation )
        {
            GameObject instance;
            if ( !TryPopValidInstance( pool, out instance ) )
                instance = Instantiate( prefab, position, rotation );
            else
            {
                instance.transform.SetPositionAndRotation( position, rotation );
                instance.SetActive( true );
            }
            return instance;
        }

        public GameObject Unpool( GameObject prefab, List<GameObject> pool, Transform parent )
        {
            GameObject instance;
            if ( !TryPopValidInstance( pool, out instance ) )
                instance = Instantiate( prefab, parent );
            else
            {
                instance.transform.SetParent( parent );
                instance.SetActive( true );
            }
            return instance;
        }

        public T PoolUnpool<T>( T prefab, List<GameObject> pool, Transform parent ) where T : MonoBehaviour
        {
            T instance;
            if ( !TryPopValidInstance( pool, out var go ) )
                instance = Instantiate( prefab, parent );
            else
            {
                go.transform.SetParent( parent );
                go.SetActive( true );
                instance = go.GetComponent<T>();
            }
            return instance;
        }
    }
}