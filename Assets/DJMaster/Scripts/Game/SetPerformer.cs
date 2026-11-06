using System;
using System.Collections.Generic;
using UnityEngine;

namespace DJMaster
{
    // Runs a set you planned in Build mode as a live show. The next song is loaded for you, a step-by-step
    // checklist (with the controls glowing) walks you through each planned transition, and every transition
    // is graded on how well you pulled off its style.
    public class SetPerformer : MonoBehaviour
    {
        public DJBoard board;
        public Mixer mixer;
        public MixJudge judge;
        [Tooltip("How many bars the last song plays before the set ends.")]
        public int finaleBars = 16;

        public enum Grade { Rough, Good, Perfect }

        public class Result
        {
            public int from, to;
            public TransitionStyle style;
            public Grade grade;
            public string note;
        }

        public class Task
        {
            public string text;
            public string[] highlight;
            public Func<bool> done;
            public bool complete;
        }

        public bool Running { get; private set; }
        public bool Finished { get; private set; }
        public SetList Set { get; private set; }
        public int SongIndex { get; private set; }          // the song that owns the dance floor
        public int OutDeck => SongIndex % 2;               // songs alternate A, B, A...
        public int InDeck => 1 - OutDeck;
        public bool InFinale => Running && SongIndex >= Set.Count - 1;
        public TransitionStyle NextStyle => Set.transitions[SongIndex];
        public int NextTrack => Set.songs[SongIndex + 1];
        public float FinaleLeft { get; private set; }
        public float Elapsed { get; private set; }
        public readonly List<Result> Results = new();
        public readonly List<Task> Tasks = new();
        public Task CurrentTask { get; private set; }
        public int CurrentTaskIndex => CurrentTask == null ? Tasks.Count : Tasks.IndexOf(CurrentTask);

        public event Action Completed;

        // What happened during the current transition, for grading.
        float overlap, synced, clash, bassClash;
        bool filterSwept, inHeard, inLowCutOnEntry, enteredOnBar;

        Deck Out => board.channels[OutDeck].deck;
        Deck In => board.channels[InDeck].deck;

        public void Begin(SetList set)
        {
            Set = set;
            Results.Clear();
            board.ResetControls();
            board.A.deck.Load(set.songs[0]);
            board.A.deck.Play();
            board.A.volume.SetValue(1f);
            board.crossfader.SetValue(0f);
            board.B.deck.Load(set.songs[1]);
            judge.ResetScore();
            SongIndex = 0;
            Elapsed = 0;
            Running = true;
            Finished = false;
            StartTransition();
        }

        public void Stop()
        {
            Running = false;
            board.ClearHighlights();
        }

        public void Finish()
        {
            if (!Running) return;
            Stop();
            Finished = true;
            Completed?.Invoke();
        }

        void Update()
        {
            if (!Running) return;
            float dt = Time.deltaTime;
            Elapsed += dt;

            if (InFinale)
            {
                FinaleLeft -= dt;
                if (FinaleLeft <= 0) Finish();
                return;
            }

            foreach (var t in Tasks)
                if (!t.complete && t.done()) t.complete = true;
            var current = Tasks.Find(t => !t.complete);
            if (current != CurrentTask)
            {
                CurrentTask = current;
                board.ClearHighlights();
                if (current?.highlight != null)
                    foreach (var id in current.highlight)
                        if (board.Find(id) is { } c) c.Highlighted = true;
            }

            Watch(dt);

            // The transition is done once the crowd only hears the new song.
            if (mixer.DominantDeck == InDeck && mixer.CrowdGain(OutDeck) < 0.05f && In.IsPlaying) CompleteTransition();
        }

        void Watch(float dt)
        {
            float gOut = mixer.CrowdGain(OutDeck), gIn = mixer.CrowdGain(InDeck);
            if (gIn > 0.15f && !inHeard)
            {
                inHeard = true;
                inLowCutOnEntry = In.Strip.low < 0.2f;
                enteredOnBar = DJMath.DistanceToBeat(Out.Beat / TrackSynth.BeatsPerBar) < 0.15f;
            }
            if (gOut > 0.15f && gIn > 0.15f)
            {
                overlap += dt;
                if (judge.InSync) synced += dt;
                if (judge.Clashing) clash += dt;
                if (Out.Strip.low > 0.35f && In.Strip.low > 0.35f) bassClash += dt;
                if (Mathf.Abs(Out.Strip.filter - 0.5f) > 0.3f) filterSwept = true;
            }
        }

        void CompleteTransition()
        {
            var style = NextStyle;
            var (grade, note) = GradeTransition(style);
            Results.Add(new Result { from = Set.songs[SongIndex], to = NextTrack, style = style, grade = grade, note = note });
            judge.Award($"{SetList.Name(style)}: {grade.ToString().ToUpper()}", grade == Grade.Perfect ? 1000 : grade == Grade.Good ? 600 : 200);
            if (grade == Grade.Perfect) BeatBus.FireDrop();

            int freed = OutDeck;
            SongIndex++;
            board.ClearHighlights();
            CurrentTask = null;
            Tasks.Clear();

            if (InFinale)
            {
                FinaleLeft = finaleBars * TrackSynth.BeatsPerBar * 60f / Mathf.Max(60f, In.BPM);
                return;
            }
            // Load the next song onto the deck that just went quiet, with its controls back at home.
            var ch = board.channels[freed];
            ch.tempo.SetValue(0.5f);
            ch.volume.SetValue(0f);
            ch.high.SetValue(0.5f); ch.mid.SetValue(0.5f); ch.low.SetValue(0.5f); ch.filter.SetValue(0.5f);
            mixer.headphoneCue[freed] = false;
            ch.deck.Load(NextTrack);
            StartTransition();
        }

        (Grade, string) GradeTransition(TransitionStyle style)
        {
            switch (style)
            {
                case TransitionStyle.Cut:
                    if (overlap < 0.6f && enteredOnBar) return (Grade.Perfect, "Right on the one!");
                    if (overlap < 2f) return (Grade.Good, enteredOnBar ? "Slam the crossfader faster." : "Hit PLAY exactly on bar 1 of the old track.");
                    return (Grade.Rough, "A cut has no overlap: PLAY and crossfader at the same moment.");
                case TransitionStyle.BassSwap:
                    if (inLowCutOnEntry && bassClash < 1.5f && clash < 1f && synced >= 3f) return (Grade.Perfect, "Clean basslines all the way.");
                    if (bassClash < 4f && clash < 3f) return (Grade.Good, inLowCutOnEntry ? "Swap the LOWs sooner." : "Cut the new track's LOW before you bring it in.");
                    return (Grade.Rough, clash >= 3f ? "The beats clashed: match TEMPO and JOG first." : "Two basslines at once got muddy.");
                case TransitionStyle.Filter:
                    if (filterSwept && clash < 1f) return (Grade.Perfect, "Great filter sweep.");
                    if (filterSwept || (clash < 1f && synced > 1.5f)) return (Grade.Good, filterSwept ? "Beatmatch before you blend." : "Sweep the old track's FILTER while you mix.");
                    return (Grade.Rough, "Beatmatch first, then sweep the FILTER.");
                default:
                    if (synced >= 5f && clash < 1f) return (Grade.Perfect, "Silky smooth.");
                    if (synced >= 1.5f && clash < 3f) return (Grade.Good, "Let both tracks play together a little longer.");
                    return (Grade.Rough, "The beats clashed: match TEMPO and JOG before you crossfade.");
            }
        }

        void StartTransition()
        {
            overlap = synced = clash = bassClash = 0;
            filterSwept = inHeard = inLowCutOnEntry = enteredOnBar = false;
            Tasks.Clear();
            CurrentTask = null;

            int o = OutDeck, n = InDeck;
            string O = o == 0 ? "A" : "B", N = n == 0 ? "A" : "B";
            string outName = BoardColors.Deck(o, $"Deck {O}"), inName = BoardColors.Deck(n, $"Deck {N}");
            var inCh = board.channels[n];
            bool XfToIn() => n == 0 ? board.crossfader.value < 0.08f : board.crossfader.value > 0.92f;

            void Add(string text, Func<bool> done, params string[] ids) => Tasks.Add(new Task { text = text, done = done, highlight = ids });
            void Play() => Add($"Press {BoardColors.Green("PLAY")} on {inName}.", () => In.IsPlaying, $"{N}.play");
            void Tempo() => Add($"Slide {inName}'s TEMPO until its BPM matches {outName}.", () => In.IsLoaded && Mathf.Abs(In.BPM - Out.BPM) < 0.35f, $"{N}.tempo");
            void Sync() => Add($"Nudge {inName}'s JOG wheel until SYNC turns green.", () => judge.InSync, $"{N}.jog");
            void Fader() => Add($"Raise {inName}'s channel fader all the way.", () => inCh.volume.value > 0.85f, $"{N}.volume");
            void Middle() => Add("Slide the crossfader to the middle: the crowd hears both.", () => board.crossfader.value > 0.3f && board.crossfader.value < 0.7f, "crossfader");
            void CrossOver() => Add($"Finish: crossfader all the way to {inName}.", XfToIn, "crossfader");

            switch (NextStyle)
            {
                case TransitionStyle.BassSwap:
                    Add($"Turn {inName}'s LOW all the way down first.", () => In.Strip.low < 0.1f, $"{N}.low");
                    Play(); Tempo(); Sync(); Fader(); Middle();
                    Add($"On a 4-bar line, swap basslines: {outName} LOW down, {inName} LOW up.", () => Out.Strip.low < 0.15f && In.Strip.low > 0.4f, $"{O}.low", $"{N}.low");
                    CrossOver();
                    break;
                case TransitionStyle.Filter:
                    Play(); Tempo(); Sync(); Fader(); Middle();
                    Add($"Sweep {outName}'s {BoardColors.Yellow("FILTER")} down to muffle it.", () => Out.Strip.filter < 0.2f || Out.Strip.filter > 0.8f, $"{O}.filter");
                    CrossOver();
                    break;
                case TransitionStyle.Cut:
                    Tempo();
                    Add($"Press {BoardColors.Orange("CUE")} on {inName} so it waits at bar 1.", () => In.IsLoaded && !In.IsPlaying && In.Beat < 0.05, $"{N}.cue");
                    Fader();
                    Add($"Count the bars on {outName}. On bar 1, 5, 9 or 13: {BoardColors.Green("PLAY")} on {inName} and SLAM the crossfader across!", () => false, $"{N}.play", "crossfader");
                    break;
                default:
                    Play(); Tempo(); Sync(); Fader(); Middle(); CrossOver();
                    break;
            }
        }

        public static int Stars(IReadOnlyList<Result> results)
        {
            if (results.Count == 0) return 0;
            int points = 0;
            foreach (var r in results) points += (int)r.grade;
            float avg = points / (float)results.Count;
            return avg >= 1.6f ? 3 : avg >= 0.9f ? 2 : 1;
        }
    }
}
