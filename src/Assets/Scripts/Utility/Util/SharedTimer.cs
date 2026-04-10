using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SharedTimer : MonoBehaviour
{
    public static SharedTimer Instance;

    Dictionary<string, Sampler> samplers = new();

    void Awake()
    {
        Instance = this;
    }

    public Sampler GetSampler( string samplerName, long ignoreFirstNSamples = 100 )
    {
        if ( !samplers.ContainsKey( samplerName ) )
            samplers[samplerName] = new Sampler() { name = samplerName, ignoreFirstNSamples = ignoreFirstNSamples };
        return samplers[samplerName];
    }

    public class Sampler
    {
        public class ScopedSample
        {
            bool running = true;
            Sampler sampler;
            internal System.TimeSpan start;

            internal ScopedSample( Sampler sampler )
            {
                this.sampler = sampler;
                start = System.DateTime.Now.TimeOfDay;
            }

            public void Stop()
            {
                if ( !running )
                    return;
                running = false;
                sampler.Stop( this );
            }
        }
        public string name = "";
        public long samples = 0;
        public long ignoreFirstNSamples = 100;
        public double milliseconds = 0;
        public double maxSample = 0;

        public void Reset()
        {
            samples = 0;
            milliseconds = 0;
            maxSample = 0;
        }

        public ScopedSample Start()
        {
            return new ScopedSample( this );
        }

        void Stop( ScopedSample sample )
        {
            var end = System.DateTime.Now.TimeOfDay;
            samples++;
            if ( samples > ignoreFirstNSamples )
            {
                milliseconds += end.TotalMilliseconds - sample.start.TotalMilliseconds;
                if ( end.TotalMilliseconds - sample.start.TotalMilliseconds > maxSample )
                    maxSample = end.TotalMilliseconds - sample.start.TotalMilliseconds;
                UnityEngine.Debug.Log( $"SharedTimer Sampler {name} avg:{( milliseconds / ( samples - ignoreFirstNSamples ) ).ToString( "0.00" )}ms | max:{maxSample:0.00}ms" );
            }
        }
    }
}