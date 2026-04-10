using System;

public static partial class Utility
{
    public class ScopedProfile : IDisposable
    {
        public ScopedProfile( string name )
        {
            UnityEngine.Profiling.Profiler.BeginSample( name );
        }

        public void Dispose()
        {
            UnityEngine.Profiling.Profiler.EndSample();
        }
    }
}