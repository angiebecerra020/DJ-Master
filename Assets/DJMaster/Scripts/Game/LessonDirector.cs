using System;
using System.Collections.Generic;
using UnityEngine;

namespace DJMaster
{
    // Learn mode. One guided session (about 7 minutes): first the board's color code, then from pressing PLAY
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
            public string[] answers;  // color quiz: touching any of these controls completes the step
            public string color;      // color quiz: the color being asked for
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
        bool quizHit;

        Deck A => board.A.deck;
        Deck B => board.B.deck;

        void Awake()
        {
            BuildSteps();
            board.tap.Pressed += OnTap;
            board.airHorn.Pressed += () => hornPressed = true;
        }

        void OnEnable() => DJControl.AnyTouched += OnTouched;
        void OnDisable() => DJControl.AnyTouched -= OnTouched;

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
            tapStreak = 0; filterWentDown = false; hornPressed = false; tapOnLine = false; quizHit = false;
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
            // Stuck on the color quiz? Point at the answers.
            if (s.answers != null && ShowHint)
                foreach (var id in s.answers)
                    if (board.Find(id) is { } c) c.Highlighted = true;
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

        void OnTouched(DJControl c)
        {
            if (!Running || praiseTimer > 0) return;
            var s = steps[StepIndex];
            if (s.answers == null) return;
            if (Array.IndexOf(s.answers, c.id) >= 0) { quizHit = true; return; }
            string touched = BoardColors.NameOf(c.id);
            if (touched == "") return;
            Feedback = $"That one is {touched}. Find something {s.color}.";
            Sfx.Fail();
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
            string blue = BoardColors.Blue("BLUE"), pink = BoardColors.Pink("PINK"), green = BoardColors.Green("GREEN"),
                orange = BoardColors.Orange("ORANGE"), yellow = BoardColors.Yellow("YELLOW"), red = BoardColors.Red("RED");
            string deckA = BoardColors.Blue("Deck A"), deckB = BoardColors.Pink("Deck B");

            // ---- The color code ----
            void Quiz(string title, string text, string color, string hint, params string[] answers) => steps.Add(new Step
            {
                title = title, text = text, hint = hint, color = color, answers = answers, done = () => quizHit,
                praise = $"Yes! That's {color}.",
            });
            Quiz("Colors: blue",
                $"Every control is color-coded, so you always know what it does. {blue} is {deckA}, the left deck. Touch anything {blue}.",
                "BLUE", "Look at the left half of the board: the ring around the big jog wheel and the HI / MID / LOW knobs.",
                "A.high", "A.mid", "A.low", "A.jog");
            Quiz("Colors: pink",
                $"{pink} is {deckB}, the right deck. Everything for the next song lives here. Touch anything {pink}.",
                "PINK", "Look at the right half of the board.",
                "B.high", "B.mid", "B.low", "B.jog");
            Quiz("Colors: yellow",
                $"{yellow} is for shaping the sound and for your ears only: the FILTER knobs and the PHONES buttons. Touch something {yellow}.",
                "YELLOW", "The yellow knobs are the lowest knob on each side of the mixer, labelled FILTER.",
                "A.filter", "B.filter", "A.headphones", "B.headphones");
            Quiz("Colors: orange",
                $"{orange} is CUE: it jumps a song back to its first beat. {green} is PLAY and {red} is for the crowd. Touch an {orange} button.",
                "ORANGE", "CUE sits right next to PLAY, at the front of each deck.",
                "A.cue", "B.cue");

            // ---- Playing a track ----
            steps.Add(new Step
            {
                title = "Welcome to the booth",
                text = $"Now you know the colors. {green} means go! Tap the {green} PLAY button on {deckA} (the {blue} side) to start your first record.",
                hint = "PLAY is at the front left of the board. Poke it with your fingertip.",
                highlight = new[] { "A.play" },
                done = () => A.IsPlaying,
                praise = "It's playing!",
                enter = () =>
                {
                    // Undo anything poked during the color quiz.
                    board.ResetControls();
                    if (A.TrackIndex != 0) A.Load(0);
                    else A.Cue();
                    B.Unload();
                },
            });
            steps.Add(new Step
            {
                title = "Volume up",
                text = $"You can't hear it yet because {deckA}'s channel fader is down. Touch the fader cap and slide it all the way up, away from you.",
                hint = "The channel fader is the vertical slider just left of center. Rest your fingertip on its cap and push.",
                highlight = new[] { "A.volume" },
                done = () => board.A.volume.value > 0.9f,
            });
            steps.Add(new Step
            {
                title = "The crossfader",
                text = $"The crossfader blends between the two decks. Slide it all the way LEFT, toward the {blue} A, to give {deckA} to the whole room.",
                hint = "The crossfader is the sideways slider at the front of the mixer.",
                highlight = new[] { "crossfader" },
                done = () => board.crossfader.value < 0.08f,
                praise = "Listen to that crowd!",
            });
            steps.Add(new Step
            {
                title = "Feel the beat",
                text = $"Dance music counts in fours: every kick drum is one beat. Tap the {red} TAP pad on each kick, four in a row.",
                hint = "Tap exactly when you hear the boom of the kick. Nod your head to find it first.",
                highlight = new[] { "tap" },
                done = () => tapStreak >= 4,
                onTap = TapOnBeat,
                praise = "You've got rhythm!",
            });
            steps.Add(new Step
            {
                title = "Count the phrases",
                text = $"Four beats make a bar, and songs change every 4 bars. Watch {deckA}'s bar counter and tap {red} TAP right as it rolls over to bar 1, 5, 9 or 13.",
                hint = "Count along: 1-2-3-4, 2-2-3-4, 3-2-3-4, 4-2-3-4... then TAP on the next 1.",
                highlight = new[] { "tap" },
                done = () => tapOnLine,
                onTap = TapOnPhrase,
                praise = "That's phrasing. Good DJs always mix on these lines.",
            });

            // ---- Preparing the next track ----
            steps.Add(new Step
            {
                title = "Load Deck B",
                text = $"Time to pick the next record. Tap the {pink} LOAD button on {deckB}.",
                hint = "LOAD is the small pink button above Deck B's jog wheel.",
                highlight = new[] { "B.load" },
                done = () => B.IsLoaded,
            });
            steps.Add(new Step
            {
                title = "Headphone cue",
                text = $"DJs prepare the next song in their headphones. Tap {deckB}'s {yellow} PHONES button so only YOU can hear it.",
                hint = "It's the yellow button under Deck B's knobs on the mixer.",
                highlight = new[] { "B.headphones" },
                done = () => mixer.headphoneCue[1],
            });
            steps.Add(new Step
            {
                title = "Start Deck B",
                text = $"Tap {green} PLAY on {deckB}. It's quieter because it's only in your headphones; the crowd still hears {deckA}.",
                highlight = new[] { "B.play" },
                done = () => B.IsPlaying,
                praise = "Two tracks at once. Hear the clash? Let's fix it.",
            });

            // ---- Beatmatching ----
            steps.Add(new Step
            {
                title = "Beatmatch: tempo",
                text = $"{deckA} runs at 128 BPM but {deckB} is slower. Slide {deckB}'s TEMPO fader up until both BPM readouts match.",
                hint = "The tempo fader is the long slider on the far right. Small moves! Watch the BPM difference shrink to 0.0.",
                highlight = new[] { "B.tempo" },
                done = () => B.IsPlaying && Mathf.Abs(B.BPM - A.BPM) < 0.35f,
                hold = 1f,
                praise = "Tempos matched!",
            });
            steps.Add(new Step
            {
                title = "Beatmatch: line up the kicks",
                text = $"Same speed, but the kicks don't land together. Spin {deckB}'s {pink} jog wheel to nudge it until the SYNC light turns green and both platter markers point the same way.",
                hint = "Spin clockwise to catch up, counter-clockwise to hold back. Short gentle spins.",
                highlight = new[] { "B.jog" },
                done = () => A.IsPlaying && B.IsPlaying && Mathf.Abs(B.BPM - A.BPM) < 0.6f && Mathf.Abs(DJMath.PhaseOffset(A.Beat, B.Beat)) < 0.07f,
                hold = 1.5f,
                praise = "Locked in! That's beatmatching, the core DJ skill.",
            });

            // ---- The mix ----
            steps.Add(new Step
            {
                title = "Make room for the bass",
                text = $"Two basslines at once sound muddy. Turn {deckB}'s LOW knob all the way down before the crowd hears it.",
                hint = "Touch the LOW knob and slide your finger toward you to turn it down.",
                highlight = new[] { "B.low" },
                done = () => board.B.low.value < 0.08f,
            });
            steps.Add(new Step
            {
                title = "Bring it in",
                text = $"Raise {deckB}'s channel fader all the way, then slide the crossfader to the middle. Now the crowd hears both songs.",
                highlight = new[] { "B.volume", "crossfader" },
                done = () => board.B.volume.value > 0.85f && board.crossfader.value > 0.35f && board.crossfader.value < 0.65f,
                enter = () => mixer.headphoneCue[1] = false,
                praise = "Two songs, one groove.",
            });
            steps.Add(new Step
            {
                title = "Bass swap",
                text = $"On the next 4-bar line, swap the basslines: turn {deckA}'s LOW down and {deckB}'s LOW up.",
                hint = "Do both knobs at once, one hand each: left hand down, right hand up.",
                highlight = new[] { "A.low", "B.low" },
                done = () => board.A.low.value < 0.15f && board.B.low.value > 0.4f,
                praise = "Smooth bass swap!",
            });
            steps.Add(new Step
            {
                title = "Finish the mix",
                text = $"Slide the crossfader all the way to the {pink} B, then pull {deckA}'s channel fader down. You just mixed your first transition!",
                highlight = new[] { "crossfader", "A.volume" },
                done = () => board.crossfader.value > 0.92f && board.A.volume.value < 0.1f,
                praise = "TRANSITION COMPLETE!",
                enter = () => Sfx.Cheer(0.6f),
            });

            // ---- Effects ----
            steps.Add(new Step
            {
                title = "Filter sweep",
                text = $"Effects build tension. Turn {deckB}'s {yellow} FILTER knob down to the left (it sounds muffled), then back to the center for a big release.",
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
                text = $"{red} is for the crowd. Every DJ needs a signature move: hit the {red} AIR HORN. Then try BUILD A SET to plan your own show!",
                highlight = new[] { "airhorn" },
                done = () => hornPressed,
                praise = "LESSON COMPLETE!",
            });
        }
    }
}
