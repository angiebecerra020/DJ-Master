using UnityEngine;

namespace DJMaster
{
    public enum GameMode { Menu, Lesson, Gig, Free, GigResults }

    // Top-level flow: menu -> DJ school / a timed gig / free play, plus the scene and comfort buttons.
    public class GameDirector : MonoBehaviour
    {
        public DJBoard board;
        public Mixer mixer;
        public MixJudge judge;
        public LessonDirector lessons;
        public InstructionPanel panel;
        public EnvironmentManager environment;
        public SeatedAnchor anchor;
        public PokeButton roomButton, clubButton, festivalButton, recenterButton, raiseButton, lowerButton;
        public float gigSeconds = 180f;

        public GameMode Mode { get; private set; }
        public float GigTimeLeft { get; private set; }

        bool paused;
        float lastScore;
        int lastTransitions;

        void Start()
        {
            TrackLibrary.Prewarm();

            panel.actionButtons[0].Pressed += () => Action(0);
            panel.actionButtons[1].Pressed += () => Action(1);
            panel.actionButtons[2].Pressed += () => Action(2);
            roomButton.Pressed += () => environment.SetMode(EnvMode.Room);
            clubButton.Pressed += () => environment.SetMode(EnvMode.Club);
            festivalButton.Pressed += () => environment.SetMode(EnvMode.Festival);
            recenterButton.Pressed += anchor.Recenter;
            raiseButton.Pressed += () => anchor.AdjustHeight(0.02f);
            lowerButton.Pressed += () => anchor.AdjustHeight(-0.02f);
            lessons.Completed += () => EnterMenu();

            OVRManager.InputFocusLost += () => SetPaused(true);
            OVRManager.InputFocusAcquired += () => SetPaused(false);

            EnterMenu();
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

        void EnterMenu()
        {
            Mode = GameMode.Menu;
            lessons.Stop();
            panel.SetActions("DJ SCHOOL", "PLAY A GIG", "FREE PLAY");
        }

        void Action(int i)
        {
            switch (Mode)
            {
                case GameMode.Menu:
                    if (i == 0) StartLesson();
                    else if (i == 1) StartGig();
                    else StartFree();
                    break;
                case GameMode.Lesson:
                    if (i == 0) EnterMenu();
                    else if (i == 1) lessons.Skip();
                    else StartLesson();
                    break;
                case GameMode.Gig:
                case GameMode.Free:
                    if (i == 0) EnterMenu();
                    else if (i == 1 && Mode == GameMode.Gig) StartGig();
                    break;
                case GameMode.GigResults:
                    if (i == 0) EnterMenu();
                    else if (i == 1) StartGig();
                    else StartFree();
                    break;
            }
        }

        void StartLesson()
        {
            Mode = GameMode.Lesson;
            panel.SetActions("MENU", "SKIP STEP", "RESTART");
            lessons.Begin();
        }

        void StartGig()
        {
            lessons.Stop();
            Mode = GameMode.Gig;
            panel.SetActions("MENU", "RESTART", null);
            board.ResetControls();
            int first = Random.Range(0, TrackLibrary.Tracks.Length);
            board.A.deck.Load(first);
            board.A.deck.Play();
            board.A.volume.SetValue(1f);
            board.crossfader.SetValue(0f);
            board.B.deck.LoadNext(first);
            judge.ResetScore();
            GigTimeLeft = gigSeconds;
        }

        void StartFree()
        {
            lessons.Stop();
            Mode = GameMode.Free;
            panel.SetActions("MENU", null, null);
            judge.ResetScore();
        }

        void EndGig()
        {
            Mode = GameMode.GigResults;
            lastScore = judge.Score;
            lastTransitions = judge.Transitions;
            int best = PlayerPrefs.GetInt("bestGig", 0);
            if (lastScore > best) { PlayerPrefs.SetInt("bestGig", (int)lastScore); PlayerPrefs.Save(); }
            Sfx.Cheer(1f);
            BeatBus.FireDrop();
            panel.SetActions("MENU", "PLAY AGAIN", "FREE PLAY");
        }

        public static int Stars(float score, int transitions) =>
            score > 4500 && transitions >= 3 ? 3 : score > 2500 && transitions >= 1 ? 2 : score > 800 ? 1 : 0;

        void Update()
        {
            if (Mode == GameMode.Gig)
            {
                GigTimeLeft -= Time.deltaTime;
                if (GigTimeLeft <= 0) EndGig();
            }

            Sfx.SetAmbience(environment.Mode == EnvMode.Room ? 0f : 0.05f + BeatBus.Energy * 0.25f);
            RefreshPanel();
        }

        void RefreshPanel()
        {
            switch (Mode)
            {
                case GameMode.Menu:
                    bool schooled = PlayerPrefs.GetInt("lessonComplete", 0) == 1;
                    int best = PlayerPrefs.GetInt("bestGig", 0);
                    panel.Show("DJ MASTER",
                        schooled
                            ? "Welcome back, DJ. Play a 3-minute gig and keep the crowd moving, or jam in free play. Use the scene buttons to switch between your room, a club and a festival."
                            : "Learn to DJ with just your hands. New here? Start DJ SCHOOL: about 6 minutes from your first PLAY to your first real mix.",
                        best > 0 ? $"Best gig: {best} pts" : "Tip: tap ROOM / CLUB / FESTIVAL below to change your surroundings.");
                    break;

                case GameMode.Lesson:
                    var s = lessons.Current;
                    if (s == null) break;
                    string body = s.text;
                    if (lessons.ShowHint && !string.IsNullOrEmpty(s.hint)) body += "\n\nHINT: " + s.hint;
                    panel.Show($"{lessons.StepIndex + 1}/{lessons.StepCount}  {s.title.ToUpper()}", body, "",
                        lessons.Feedback);
                    break;

                case GameMode.Gig:
                    panel.Show($"LIVE  {TextUtil.Clock(GigTimeLeft)}",
                        judge.Warning != "" ? judge.Warning : "Keep the floor moving: beatmatch the next track, blend it in on a phrase, and swap the bass.",
                        $"Score {judge.Score:0}   Transitions {judge.Transitions}   Crowd {(int)(BeatBus.Energy * 100)}%",
                        judge.Shoutout);
                    break;

                case GameMode.Free:
                    panel.Show("FREE PLAY",
                        judge.Warning != "" ? judge.Warning : "No timer, no pressure. Load tracks, try bass drops (cut the LOW, slam it back on bar 1) and filter sweeps.",
                        $"Crowd {(int)(BeatBus.Energy * 100)}%", judge.Shoutout);
                    break;

                case GameMode.GigResults:
                    int stars = Stars(lastScore, lastTransitions);
                    panel.Show("SET COMPLETE!",
                        $"Score {lastScore:0}  with {lastTransitions} transition{(lastTransitions == 1 ? "" : "s")}.\n" +
                        (stars == 3 ? "Headliner material. The crowd wants an encore!" : stars == 2 ? "Solid set! Mix more often to reach headliner level." : "Keep practicing: beatmatch, then blend on a phrase line."),
                        $"Best: {PlayerPrefs.GetInt("bestGig", 0)} pts", new string('*', stars) + new string('-', 3 - stars));
                    break;
            }
        }
    }
}
