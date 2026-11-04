using System;
using UnityEngine;

namespace DJMaster
{
    // A virtual CDJ: plays one looping track, with tempo control, cue and jog-wheel pitch bend.
    [RequireComponent(typeof(AudioSource), typeof(ChannelStrip))]
    public class Deck : MonoBehaviour
    {
        public string deckName = "A";
        [Range(0, 1)] public float tempoFader = 0.5f; // 0.5 = original tempo

        public AudioSource Source { get; private set; }
        public ChannelStrip Strip { get; private set; }

        public int TrackIndex { get; private set; } = -1;
        public bool HasTrack => TrackIndex >= 0;
        public TrackInfo Track => HasTrack ? TrackLibrary.Tracks[TrackIndex] : default;
        public bool IsLoaded => clip != null;
        public bool IsLoading => HasTrack && clip == null;
        public bool IsPlaying => IsLoaded && (Source.isPlaying || pendingPlay);

        public float Rate => DJMath.TempoRate(tempoFader);
        public float TempoPercent => (Rate - 1f) * 100f;
        public float BPM => HasTrack ? Track.bpm * Rate : 0f;

        // Position in beats since the start of the loop (0 .. TotalBeats).
        public double Beat => IsLoaded ? Source.timeSamples * (double)Track.bpm / (60.0 * clip.frequency) : 0;
        public int Bar => (int)(Beat / TrackSynth.BeatsPerBar) % TrackSynth.Bars;          // 0-based
        public int BeatInBar => (int)Beat % TrackSynth.BeatsPerBar;                         // 0-based
        public TrackSynth.Section Section => TrackSynth.SectionOfBar(Bar);

        public event Action<Deck> Loaded;
        public event Action<Deck, int> BeatTick; // fires on every whole beat while playing

        AudioClip clip;
        bool pendingPlay;
        float jogBend;   // set every frame the jog wheel is touched
        int lastWholeBeat = -1;

        void Awake()
        {
            Source = GetComponent<AudioSource>();
            Strip = GetComponent<ChannelStrip>();
            Source.playOnAwake = false;
            Source.loop = true;
            Source.spatialBlend = 0f;
            Source.volume = 1f;
        }

        public void Load(int index)
        {
            Source.Stop();
            Source.clip = null;
            clip = null;
            pendingPlay = false;
            lastWholeBeat = -1;
            TrackIndex = ((index % TrackLibrary.Tracks.Length) + TrackLibrary.Tracks.Length) % TrackLibrary.Tracks.Length;
            TrackLibrary.Render(TrackIndex);
            TryFinishLoad();
        }

        public void Unload()
        {
            Source.Stop();
            Source.clip = null;
            clip = null;
            pendingPlay = false;
            TrackIndex = -1;
        }

        public void LoadNext(int otherDeckTrack)
        {
            int next = TrackIndex + 1;
            if (next % TrackLibrary.Tracks.Length == otherDeckTrack) next++;
            Load(next);
        }

        void TryFinishLoad()
        {
            var c = TrackLibrary.TryGetClip(TrackIndex);
            if (c == null) return;
            clip = c;
            Source.clip = clip;
            Source.timeSamples = 0;
            Loaded?.Invoke(this);
            if (pendingPlay) { pendingPlay = false; Source.Play(); }
        }

        public void TogglePlay()
        {
            if (!HasTrack) return;
            if (!IsLoaded) { pendingPlay = !pendingPlay; return; }
            if (Source.isPlaying) Source.Pause();
            else Source.Play();
        }

        public void Play() { if (!IsPlaying) TogglePlay(); }
        public void Pause() { if (IsPlaying) TogglePlay(); }

        // CUE: stop and return to the top of the track (bar 1, the drop), ready to start on the beat.
        public void Cue()
        {
            if (!IsLoaded) return;
            Source.Pause();
            Source.timeSamples = 0;
            lastWholeBeat = -1;
        }

        // Jog wheel. Playing: temporary pitch bend from spin speed. Paused: scrub (1 revolution = 1 bar).
        public void Jog(float revolutionsThisFrame, float dt)
        {
            if (!IsLoaded) return;
            if (Source.isPlaying)
            {
                float revPerSec = dt > 0 ? revolutionsThisFrame / dt : 0;
                jogBend = Mathf.Clamp(revPerSec * 0.12f, -0.25f, 0.25f);
            }
            else
            {
                double beats = revolutionsThisFrame * TrackSynth.BeatsPerBar;
                long samples = (long)(beats * 60.0 / Track.bpm * clip.frequency);
                long t = (Source.timeSamples + samples) % clip.samples;
                if (t < 0) t += clip.samples;
                Source.timeSamples = (int)t;
            }
        }

        void Update()
        {
            if (IsLoading) TryFinishLoad();
            if (!IsLoaded) return;

            Source.pitch = Rate * (1f + jogBend);
            jogBend = Mathf.MoveTowards(jogBend, 0, Time.deltaTime * 2f);

            if (Source.isPlaying)
            {
                int whole = (int)Beat;
                if (whole != lastWholeBeat)
                {
                    lastWholeBeat = whole;
                    BeatTick?.Invoke(this, whole);
                }
            }
        }
    }
}
