using System;
using System.Collections.Generic;
using UnityEngine;

namespace DJMaster
{
    // The DJ school. One guided session (about 6 minutes) that takes you from pressing PLAY
    // to beatmatching and finishing a full transition, one control at a time.
    public class LessonDirector : MonoBehaviour
    {
        public DJBoard board;
        public Mixer mixer;
        public MixJudge judge;

        public class Step
        {
            public string title;
            public string text;
            public string hint;
            public string[] highlight;
            public Func<bool> done;
            public float hold;        // condition must stay true this long
            public string praise = "Nice!";
            public Action enter;
            public Action onTap;      // what the TAP pad does during this step
        }

        public bool Running { get; private set; }
        public bool Finished { get; private set; }
        public int StepIndex { get; private set; }
        public int StepCount => steps.Count;
        public Step Current => Running && StepIndex < steps.Count ? steps[StepIndex] : null;
        public string Feedback { get; private set; } = "";
        public bool ShowingPraise => praiseTimer > 0;
        public bool ShowHint => stepTime > 25f;

        public event Action Completed;

        readonly List<Step> steps = new();
        float heldFor, stepTime, praiseTimer;

        // Step-local state
        int tapStreak;
        bool filterWentDown;
        bool hornPressed;
        bool tapOnLine;

        Deck A => board.A.deck;
        Deck B => board.B.deck;

        void Awake()
        {
            BuildSteps();
            board.tap.Pressed += OnTap;
            board.airHorn.Pressed += () => hornPressed = true;
        }

        public void Begin()
        {
            board.ResetControls();
            A.Load(0);  // Neon Tides, 128 BPM house
            B.Unload();
            Running = true;
            Finished = false;
            Goto(0);
        }

        public void Stop()
        {
            Running = false;
            board.ClearHighlights();
        }

        public void Skip()
        {
            if (!Running) return;
            praiseTimer = 0;
            Next();
        }

        void Goto(int i)
        {
            StepIndex = i;
            heldFor = 0; stepTime = 0;
            tapStreak = 0; filterWentDown = false; hornPressed = false; tapOnLine = false;
            Feedback = "";
            board.ClearHighlights();
            if (i >= steps.Count) { Finish(); return; }
            var s = steps[i];
            s.enter?.Invoke();
            if (s.highlight != null)
                foreach (var id in s.highlight)
                    if (board.Find(id) is { } c) c.Highlighted = true;
        }

        void Next() => Goto(StepIndex + 1);

        void Finish()
        {
            Running = false;
            Finished = true;
            PlayerPrefs.SetInt("lessonComplete", 1);
            PlayerPrefs.Save();
            Sfx.Cheer(1f);
            BeatBus.FireDrop();
            Completed?.Invoke();
        }

        void Update()
        {
            if (!Running) return;
            if (praiseTimer > 0)
            {
                praiseTimer -= Time.deltaTime;
                if (praiseTimer <= 0) Next();
                return;
            }

            var s = steps[StepIndex];
            stepTime += Time.deltaTime;
            bool done = s.done();
            heldFor = done ? heldFor + Time.deltaTime : 0;
            if (done && heldFor >= s.hold)
            {
                Sfx.Success();
                Feedback = s.praise;
                praiseTimer = 1.6f;
                board.ClearHighlights();
            }
        }

        void OnTap()
        {
            if (!Running || praiseTimer > 0 || !A.IsPlaying) return;
            steps[StepIndex].onTap?.Invoke();
        }

        void TapOnBeat()
        {
            if (DJMath.DistanceToBeat(A.Beat) < 0.2f) { tapStreak++; Feedback = new string('*', tapStreak) + "  on beat!"; }
            else { tapStreak = 0; Feedback = "Off the beat. Listen for the kick drum and try again."; Sfx.Fail(); }
        }

        void TapOnPhrase()
        {
            double bar = A.Beat / TrackSynth.BeatsPerBar;
            int nearestBar = (int)Math.Round(bar) % TrackSynth.Bars;
            bool onBarLine = DJMath.DistanceToBeat(bar) < 0.07f; // within about a quarter beat
            if (onBarLine && nearestBar % 4 == 0) { tapOnLine = true; return; }
            Feedback = onBarLine ? $"That was bar {nearestBar + 1}. Wait for bar 1, 5, 9 or 13." : "Not quite on the line. Count 1-2-3-4 with the bar counter.";
            Sfx.Fail();
        }

        void BuildSteps()
        {
            steps.Add(new Step
            {
                title = "Welcome to the booth",
                text = "This is a DJ controller: two decks with a mixer in the middle. Tap PLAY on Deck A (the left deck) to start your first record.",
                hint = "The PLAY button is the round green-lit button at the bottom left. Poke it with your fingertip.",
                highlight = new[] { "A.play" },
                done = () => A.IsPlaying,
                praise = "It's playing!",
            });
            steps.Add(new Step
            {
                title = "Volume up",
                text = "You can't hear it yet because Deck A's channel fader is down. Touch the fader cap and slide it all the way up, away from you.",
                hint = "The channel fader is the vertical slider just left of center. Rest your fingertip on its cap and push.",
                highlight = new[] { "A.volume" },
                done = () => board.A.volume.value > 0.9f,
            });
            steps.Add(new Step
            {
                title = "The crossfader",
                text = "The crossfader blends between the two decks. Slide it all the way LEFT to give Deck A to the whole room.",
                hint = "The crossfader is the sideways slider at the front of the mixer.",
                highlight = new[] { "crossfader" },
                done = () => board.crossfader.value < 0.08f,
                praise = "Listen to that crowd!",
            });
            steps.Add(new Step
            {
                title = "Feel the beat",
                text = "Dance music counts in fours: every kick drum is one beat. Tap the TAP pad on each kick, four in a row. The beat lights on the screen help.",
                hint = "Tap exactly when you hear the boom of the kick. Nod your head to find it first.",
                highlight = new[] { "tap" },
                done = () => tapStreak >= 4,
                onTap = TapOnBeat,
                praise = "You've got rhythm!",
            });
            steps.Add(new Step
            {
                title = "Count the phrases",
                text = "Four beats make a bar, and tracks change every 4 bars. Watch Deck A's bar counter and tap TAP right as it rolls over to bar 1, 5, 9 or 13.",
                hint = "Count along: 1-2-3-4, 2-2-3-4, 3-2-3-4, 4-2-3-4... then TAP on the next 1.",
                highlight = new[] { "tap" },
                done = () => tapOnLine,
                onTap = TapOnPhrase,
                praise = "That's phrasing. Good DJs always mix on these lines.",
            });
            steps.Add(new Step
            {
                title = "Load Deck B",
                text = "Time to pick the next record. Tap LOAD on Deck B (the right deck).",
                hint = "LOAD is the small button above Deck B's jog wheel.",
                highlight = new[] { "B.load" },
                done = () => B.IsLoaded,
            });
            steps.Add(new Step
            {
                title = "Headphone cue",
                text = "DJs prepare the next track in their headphones. Tap Deck B's headphone (CUE) button so only YOU can hear Deck B.",
                hint = "It's the button with the headphone label, under Deck B's knobs on the mixer.",
                highlight = new[] { "B.headphones" },
                done = () => mixer.headphoneCue[1],
            });
            steps.Add(new Step
            {
                title = "Start Deck B",
                text = "Tap PLAY on Deck B. It's quieter because it's only in your headphones; the crowd still hears Deck A.",
                highlight = new[] { "B.play" },
                done = () => B.IsPlaying,
                praise = "Two tracks at once. Hear the clash? Let's fix it.",
            });
            steps.Add(new Step
            {
                title = "Beatmatch: tempo",
                text = "Deck A runs at 128 BPM but Deck B is slower. Slide Deck B's TEMPO fader up until both BPM readouts match.",
                hint = "The tempo fader is the long slider on the far right. Small moves! Watch the BPM difference shrink to 0.0.",
                highlight = new[] { "B.tempo" },
                done = () => B.IsPlaying && Mathf.Abs(B.BPM - A.BPM) < 0.35f,
                hold = 1f,
                praise = "Tempos matched!",
            });
            steps.Add(new Step
            {
                title = "Beatmatch: line up the kicks",
                text = "Same speed, but the kicks don't land together. Spin Deck B's jog wheel to nudge it until the SYNC light turns green and both platter markers point the same way.",
                hint = "Spin clockwise to catch up, counter-clockwise to hold back. Short gentle spins.",
                highlight = new[] { "B.jog" },
                done = () => A.IsPlaying && B.IsPlaying && Mathf.Abs(B.BPM - A.BPM) < 0.6f && Mathf.Abs(DJMath.PhaseOffset(A.Beat, B.Beat)) < 0.07f,
                hold = 1.5f,
                praise = "Locked in! That's beatmatching, the core DJ skill.",
            });
            steps.Add(new Step
            {
                title = "Make room for the bass",
                text = "Two basslines at once sound muddy. Turn Deck B's LOW knob all the way down before the crowd hears it.",
                hint = "Touch the LOW knob and slide your finger toward you to turn it down.",
                highlight = new[] { "B.low" },
                done = () => board.B.low.value < 0.08f,
            });
            steps.Add(new Step
            {
                title = "Bring it in",
                text = "Raise Deck B's channel fader all the way, then slide the crossfader to the middle. Now the crowd hears both tracks.",
                highlight = new[] { "B.volume", "crossfader" },
                done = () => board.B.volume.value > 0.85f && board.crossfader.value > 0.35f && board.crossfader.value < 0.65f,
                enter = () => mixer.headphoneCue[1] = false,
                praise = "Two tracks, one groove.",
            });
            steps.Add(new Step
            {
                title = "Bass swap",
                text = "On the next 4-bar line, swap the basslines: turn Deck A's LOW down and Deck B's LOW up.",
                hint = "Do both knobs at once, one hand each: left hand down, right hand up.",
                highlight = new[] { "A.low", "B.low" },
                done = () => board.A.low.value < 0.15f && board.B.low.value > 0.4f,
                praise = "Smooth bass swap!",
            });
            steps.Add(new Step
            {
                title = "Finish the mix",
                text = "Slide the crossfader all the way to Deck B, then pull Deck A's channel fader down. You just mixed your first transition!",
                highlight = new[] { "crossfader", "A.volume" },
                done = () => board.crossfader.value > 0.92f && board.A.volume.value < 0.1f,
                praise = "TRANSITION COMPLETE!",
                enter = () => Sfx.Cheer(0.6f),
            });
            steps.Add(new Step
            {
                title = "Filter sweep",
                text = "Effects build tension. Turn Deck B's FILTER knob down to the left (it sounds muffled), then back to the center for a big release.",
                hint = "Center is straight up. Double-tap any knob to snap it back to center.",
                highlight = new[] { "B.filter" },
                done = () =>
                {
                    if (board.B.filter.value < 0.2f) filterWentDown = true;
                    return filterWentDown && Mathf.Abs(board.B.filter.value - 0.5f) < 0.06f;
                },
                praise = "Big release!",
            });
            steps.Add(new Step
            {
                title = "Drop it!",
                text = "Every DJ needs a signature move. Hit the AIR HORN. You're a DJ now!",
                highlight = new[] { "airhorn" },
                done = () => hornPressed,
                praise = "LESSON COMPLETE!",
            });
        }
    }
}
