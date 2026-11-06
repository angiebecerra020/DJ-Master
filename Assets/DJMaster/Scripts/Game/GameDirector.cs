using UnityEngine;

namespace DJMaster
{
    public enum GameMode { Title, ChooseVenue, ChooseLesson, Learn, Build, Live, LiveResults, Freestyle }

    // Top-level flow: pick a mode (Learn / Build a set / Freestyle), then pick where to play
    // (your room, a club or a festival). Also wires up the scene and comfort buttons.
    public class GameDirector : MonoBehaviour
    {
        public DJBoard board;
        public Mixer mixer;
        public MixJudge judge;
        public LessonDirector lessons;
        public SetPerformer performer;
        public MixRecorder recorder;
        public InstructionPanel panel;
        public SetBuilderPanel builder;
        public EnvironmentManager environment;
        public SeatedAnchor anchor;
        public PokeButton roomButton, clubButton, festivalButton, recenterButton, raiseButton, lowerButton;

        public GameMode Mode { get; private set; }
        public GameMode ChosenMode { get; private set; } // what starts after the venue is picked

        bool paused;
        float lastScore;
        int replayIndex;

        void Start()
        {
            TrackLibrary.Prewarm();

            for (int i = 0; i < panel.actionButtons.Length; i++)
            {
                int idx = i;
                panel.actionButtons[i].Pressed += () => Action(idx);
            }
            roomButton.Pressed += () => environment.SetMode(EnvMode.Room);
            clubButton.Pressed += () => environment.SetMode(EnvMode.Club);
            festivalButton.Pressed += () => environment.SetMode(EnvMode.Festival);
            recenterButton.Pressed += anchor.Recenter;
            raiseButton.Pressed += () => anchor.AdjustHeight(0.02f);
            lowerButton.Pressed += () => anchor.AdjustHeight(-0.02f);
            lessons.Completed += ChooseLesson;
            performer.Completed += ShowResults;
            builder.BackPressed += EnterTitle;
            builder.GoLivePressed += StartLive;

            OVRManager.InputFocusLost += () => SetPaused(true);
            OVRManager.InputFocusAcquired += () => SetPaused(false);

            EnterTitle();
            // Something to listen to straight away while you look around the menu.
            board.A.deck.Load(0);
            board.B.deck.Load(1);
        }

        void OnApplicationPause(bool pause) => SetPaused(pause);
        void OnApplicationFocus(bool focus) { if (!Application.isEditor) SetPaused(!focus); }

        void SetPaused(bool p)
        {
            if (paused == p) return;
            paused = p;
            AudioListener.pause = p;
            Time.timeScale = p ? 0f : 1f;
        }

        void SetMode(GameMode mode)
        {
            Mode = mode;
            bool building = mode == GameMode.Build;
            panel.gameObject.SetActive(!building);
            builder.gameObject.SetActive(building);
        }

        void EnterTitle()
        {
            lessons.Stop();
            performer.Stop();
            SetMode(GameMode.Title);
            panel.SetActions("LEARN", "BUILD A SET", "FREESTYLE");
        }

        void ChooseVenue(GameMode next)
        {
            ChosenMode = next;
            SetMode(GameMode.ChooseVenue);
            panel.SetActions("MY ROOM", "CLUB", "FESTIVAL", "BACK");
        }

        void ChooseLesson()
        {
            lessons.Stop();
            SetMode(GameMode.ChooseLesson);
            panel.SetActions("1 FIRST MIX", "2 FADE & CUT", "3 SYNC & FX", "MENU");
        }

        void Action(int i)
        {
            switch (Mode)
            {
                case GameMode.Title:
                    ChooseVenue(i == 0 ? GameMode.Learn : i == 1 ? GameMode.Build : GameMode.Freestyle);
                    break;
                case GameMode.ChooseVenue:
                    if (i == 3) { EnterTitle(); break; }
                    environment.SetMode(i == 0 ? EnvMode.Room : i == 1 ? EnvMode.Club : EnvMode.Festival);
                    if (ChosenMode == GameMode.Learn) ChooseLesson();
                    else if (ChosenMode == GameMode.Build) StartBuild();
                    else StartFreestyle();
                    break;
                case GameMode.ChooseLesson:
                    if (i == 3) EnterTitle();
                    else StartLearn(i);
                    break;
                case GameMode.Learn:
                    if (i == 0) ChooseLesson();
                    else if (i == 1) lessons.Skip();
                    else StartLearn(lessons.LessonIndex);
                    break;
                case GameMode.Live:
                    if (i == 0) EnterTitle();
                    else if (i == 1) StartLive();
                    else performer.Finish();
                    break;
                case GameMode.LiveResults:
                    if (i != 3) recorder.StopReplay();
                    if (i == 0) EnterTitle();
                    else if (i == 1) StartLive();
                    else if (i == 2) StartBuild();
                    else ListenBack();
                    break;
                case GameMode.Freestyle:
                    EnterTitle();
                    break;
            }
        }

        void StartLearn(int lesson)
        {
            performer.Stop();
            SetMode(GameMode.Learn);
            panel.SetActions("LESSONS", "SKIP STEP", "RESTART");
            lessons.Begin(lesson);
        }

        // Replays each recorded transition in turn, with the decks paused so you hear only the recording.
        void ListenBack()
        {
            var results = performer.Results;
            for (int tries = 0; tries < results.Count; tries++)
            {
                var r = results[replayIndex++ % results.Count];
                if (r.recording == null) continue;
                board.A.deck.Pause();
                board.B.deck.Pause();
                recorder.Play(r.recording);
                replaying = $"Listening back: {SetList.Name(r.style)} into {Title(r.to)}";
                return;
            }
            replaying = "No recordings yet.";
        }

        string replaying = "";

        void StartBuild()
        {
            lessons.Stop();
            performer.Stop();
            SetMode(GameMode.Build);
            // Songs you add play on Deck A so you can hear your picks.
            board.ResetControls();
            board.A.volume.SetValue(1f);
            board.crossfader.SetValue(0f);
            board.B.deck.Pause();
        }

        void StartLive()
        {
            if (!builder.Set.Ready) { StartBuild(); return; }
            lessons.Stop();
            SetMode(GameMode.Live);
            panel.SetActions("MENU", "RESTART", "END SET");
            performer.Begin(builder.Set);
        }

        void StartFreestyle()
        {
            lessons.Stop();
            performer.Stop();
            SetMode(GameMode.Freestyle);
            panel.SetActions("MENU");
            judge.ResetScore();
        }

        void ShowResults()
        {
            SetMode(GameMode.LiveResults);
            lastScore = judge.Score;
            if (lastScore > PlayerPrefs.GetInt("bestSet", 0)) { PlayerPrefs.SetInt("bestSet", (int)lastScore); PlayerPrefs.Save(); }
            Sfx.Cheer(1f);
            BeatBus.FireDrop();
            replayIndex = 0;
            replaying = "";
            panel.SetActions("MENU", "PLAY AGAIN", "EDIT SET", "LISTEN BACK");
        }

        void Update()
        {
            Sfx.SetAmbience(environment.Mode == EnvMode.Room ? 0f : 0.05f + BeatBus.Energy * 0.25f);
            RefreshPanel();
        }

        static string Title(int track) => TrackLibrary.Tracks[track].title;

        void RefreshPanel()
        {
            switch (Mode)
            {
                case GameMode.Title:
                    bool schooled = PlayerPrefs.GetInt("lessonComplete", 0) == 1;
                    int best = PlayerPrefs.GetInt("bestSet", 0);
                    panel.Show("DJ MASTER",
                        "Choose a mode:\n" +
                        $"{BoardColors.Blue("LEARN")}  three short lessons: your first mix, fades and cuts, sync, loops and FX.\n" +
                        $"{BoardColors.Pink("BUILD A SET")}  pick songs and transitions, then perform them live.\n" +
                        $"{BoardColors.Yellow("FREESTYLE")}  just play, no rules.",
                        best > 0 ? $"Best live set: {best} pts" : schooled ? "" : "New to DJing? Start with LEARN, lesson 1 (about 7 minutes).");
                    break;

                case GameMode.ChooseVenue:
                    string what = ChosenMode == GameMode.Learn ? "LEARN" : ChosenMode == GameMode.Build ? "BUILD A SET" : "FREESTYLE";
                    panel.Show("WHERE DO YOU WANT TO PLAY?",
                        "MY ROOM  keep seeing your real surroundings (mixed reality).\n" +
                        "CLUB  a dark club with lasers, a mirror ball and a packed dance floor.\n" +
                        "FESTIVAL  a night-time main stage with fireworks and a huge crowd.",
                        $"Mode: {what}.  You can switch anytime with the SCENE panel on your left.");
                    break;

                case GameMode.ChooseLesson:
                    string Line(int n, string what) => $"{(LessonDirector.IsDone(n) ? BoardColors.Green("DONE ") : "")}{n + 1}  {LessonDirector.LessonNames[n]}: {what}";
                    panel.Show("LEARN: PICK A LESSON",
                        Line(0, "the color code, the beat, and your first beatmix (7 min)") + "\n" +
                        Line(1, "the DJ loop, fades and cuts. No beatmatching needed (4 min)") + "\n" +
                        Line(2, "tap tempo, SYNC, an 8-bar mix, loops and echo (5 min)"),
                        "New? Start with lesson 1.");
                    break;

                case GameMode.Learn:
                    var s = lessons.Current;
                    if (s == null) break;
                    string body = s.text;
                    if (lessons.ShowHint && !string.IsNullOrEmpty(s.hint)) body += "\n\nHINT: " + s.hint;
                    panel.Show($"LESSON {lessons.LessonIndex + 1}  {lessons.StepIndex + 1}/{lessons.StepCount}  {s.title.ToUpper()}", body, "", lessons.Feedback);
                    break;

                case GameMode.Live:
                    if (!performer.Running) break;
                    var set = performer.Set;
                    string title = $"LIVE  SONG {performer.SongIndex + 1}/{set.Count}  {TextUtil.Clock(performer.Elapsed)}";
                    string text;
                    if (performer.InFinale)
                        text = $"Last song: {Title(set.songs[performer.SongIndex])}. Enjoy it! Try a {BoardColors.Yellow("FILTER")} sweep, a bass drop or the {BoardColors.Red("AIR HORN")}.\nSet ends in {TextUtil.Clock(performer.FinaleLeft)}.";
                    else
                    {
                        var task = performer.CurrentTask;
                        text = $"NEXT: {Title(performer.NextTrack)} with a {BoardColors.Yellow(SetList.Name(performer.NextStyle))}\n" +
                               (task != null ? $"Step {performer.CurrentTaskIndex + 1}/{performer.Tasks.Count}: {task.text}" : "");
                    }
                    if (judge.Warning != "") text += "\n" + BoardColors.Red(judge.Warning);
                    panel.Show(title, text, $"Score {judge.Score:0}   Crowd {(int)(BeatBus.Energy * 100)}%", judge.Shoutout);
                    break;

                case GameMode.LiveResults:
                    var results = performer.Results;
                    int stars = SetPerformer.Stars(results);
                    var sb = new System.Text.StringBuilder();
                    foreach (var r in results)
                    {
                        string grade = r.grade == SetPerformer.Grade.Perfect ? BoardColors.Green("PERFECT") : r.grade == SetPerformer.Grade.Good ? BoardColors.Yellow("GOOD") : BoardColors.Red("ROUGH");
                        sb.Append($"{SetList.Name(r.style)}: {grade}. {r.note}\n");
                    }
                    if (results.Count < performer.Set.Count - 1) sb.Append("You ended the set early.\n");
                    var c = performer.Review;
                    sb.Append($"LISTEN BACK: beats in sync {c.syncPercent:0}% of mix time, {c.clashSeconds:0} s of clashing, {c.deadAirSeconds:0} s of silence.\n");
                    sb.Append($"PRACTISE NEXT: {c.practise}");
                    bool listening = recorder.Replaying;
                    panel.Show("SET COMPLETE!", sb.ToString(),
                        listening ? replaying : $"Score {lastScore:0}   Best {PlayerPrefs.GetInt("bestSet", 0)} pts",
                        new string('*', stars) + new string('-', 3 - stars));
                    break;

                case GameMode.Freestyle:
                    panel.Show("FREESTYLE",
                        judge.Warning != "" ? judge.Warning : "No timer, no rules. LOAD songs, mix with any transition, try bass drops (cut the LOW, slam it back on bar 1), LOOPs and ECHO. Remember: less is more.",
                        $"Crowd {(int)(BeatBus.Energy * 100)}%{(board.TapBpm > 0 ? $"   Tapped {board.TapBpm:0.0} BPM" : "")}", board.SyncMessage != "" ? board.SyncMessage : judge.Shoutout);
                    break;
            }
        }
    }
}
