using System;
using System.Collections.Generic;

namespace DJMaster
{
    public enum TransitionStyle { Blend, BassSwap, Filter, Cut }

    // A planned DJ set: songs in order, and the transition style used to mix into each song after the first.
    [Serializable]
    public class SetList
    {
        public const int MinSongs = 2;
        public const int MaxSongs = 5;

        public readonly List<int> songs = new();
        public readonly List<TransitionStyle> transitions = new(); // transitions[i] mixes songs[i] into songs[i + 1]

        public int Count => songs.Count;
        public bool Ready => songs.Count >= MinSongs;
        public bool Full => songs.Count >= MaxSongs;

        public static string Name(TransitionStyle t) => t switch
        {
            TransitionStyle.BassSwap => "BASS SWAP",
            TransitionStyle.Filter => "FILTER FADE",
            TransitionStyle.Cut => "QUICK CUT",
            _ => "BLEND",
        };

        public static string Describe(TransitionStyle t) => t switch
        {
            TransitionStyle.BassSwap => "Play both tracks, then trade their basslines on a phrase line. The classic house mix.",
            TransitionStyle.Filter => "Muffle the old track with its FILTER while the new one comes in. Builds tension.",
            TransitionStyle.Cut => "No overlap: wait for bar 1, hit PLAY and slam the crossfader. Fast and punchy.",
            _ => "Beatmatch, then slowly crossfade from one track to the other. Smooth and simple.",
        };

        // Returns an error to show the player, or null if the song was added.
        public string Add(int track, TransitionStyle into)
        {
            if (Full) return $"Your set is full ({MaxSongs} songs). GO LIVE!";
            if (track < 0 || track >= TrackLibrary.Tracks.Length) return "Unknown track.";
            if (songs.Contains(track)) return $"{TrackLibrary.Tracks[track].title} is already in your set.";
            if (songs.Count > 0) transitions.Add(into);
            songs.Add(track);
            return null;
        }

        public void Undo()
        {
            if (songs.Count == 0) return;
            songs.RemoveAt(songs.Count - 1);
            if (transitions.Count > 0 && transitions.Count >= songs.Count) transitions.RemoveAt(transitions.Count - 1);
        }

        public void Clear() { songs.Clear(); transitions.Clear(); }

        // Compact form for PlayerPrefs, e.g. "0,1,3|1,3".
        public string Serialize() => string.Join(",", songs) + "|" + string.Join(",", transitions.ConvertAll(t => (int)t));

        public static SetList Parse(string s)
        {
            var set = new SetList();
            if (string.IsNullOrEmpty(s)) return set;
            var parts = s.Split('|');
            var styles = new List<TransitionStyle>();
            if (parts.Length > 1)
                foreach (var t in parts[1].Split(','))
                    if (int.TryParse(t, out int v) && Enum.IsDefined(typeof(TransitionStyle), v)) styles.Add((TransitionStyle)v);
            int n = 0;
            foreach (var p in parts[0].Split(','))
            {
                if (!int.TryParse(p, out int track)) continue;
                var style = n > 0 && n - 1 < styles.Count ? styles[n - 1] : TransitionStyle.Blend;
                if (set.Add(track, style) == null) n++;
            }
            return set;
        }
    }
}
