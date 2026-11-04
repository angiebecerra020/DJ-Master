using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace DJMaster
{
    // The crate of records. Every track is synthesized on a worker thread the first time it's needed.
    public static class TrackLibrary
    {
        public static readonly TrackInfo[] Tracks =
        {
            new("Neon Tides",       "Lumen Kid",       128f, 9,  Genre.House,  101),
            new("Velvet Static",    "Night Bus Audio", 124f, 2,  Genre.Disco,  202),
            new("Concrete Bloom",   "Ferro",           130f, 5,  Genre.Techno, 303),
            new("Sundial",          "Mira Okafor",     122f, 7,  Genre.House,  404),
            new("Glass Arcade",     "Pocket Rocket",   126f, 0,  Genre.Disco,  505),
            new("Low Orbit",        "Ferro",           128f, 4,  Genre.Techno, 606),
            new("Afterglow Avenue", "Lumen Kid",       125f, 11, Genre.House,  707),
            new("Pulse Garden",     "Mira Okafor",     127f, 3,  Genre.Techno, 808),
        };

        static readonly ConcurrentDictionary<int, Task<float[]>> renders = new();
        static readonly Dictionary<int, AudioClip> clips = new();

        public static Task<float[]> Render(int index) =>
            renders.GetOrAdd(index, i => Task.Run(() => TrackSynth.Render(Tracks[i])));

        // Start rendering everything in the background so LOAD is instant later.
        public static void Prewarm()
        {
            for (int i = 0; i < Tracks.Length; i++) Render(i);
        }

        // Main thread only. Returns null until the render has finished.
        public static AudioClip TryGetClip(int index)
        {
            if (clips.TryGetValue(index, out var clip) && clip != null) return clip;
            var task = Render(index);
            if (!task.IsCompleted) return null;
            if (task.IsFaulted) { Debug.LogException(task.Exception); return null; }

            float[] data = task.Result;
            clip = AudioClip.Create(Tracks[index].title, data.Length / 2, 2, TrackSynth.SampleRate, false);
            clip.SetData(data, 0);
            clips[index] = clip;
            return clip;
        }
    }
}
