using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // Builds a three-song set in Build mode, then performs it live: a blend and a quick cut, both done right.
    public class LiveSetTest
    {
        [UnitySetUp]
        public IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/DJMaster.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            yield return null;
        }

        static IEnumerator WaitFor(System.Func<bool> cond, float seconds, string what)
        {
            float t = 0;
            while (!cond() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(cond(), "timed out waiting for: " + what);
        }

        static void MatchTempo(DeckControls ch, float bpm) =>
            ch.tempo.SetValue(0.5f + (bpm / ch.deck.Track.bpm - 1f) / (2f * DJMath.TempoRange));

        // What a perfect jog-wheel nudge does: put the incoming deck's kicks exactly on the playing deck's.
        static void LineUp(Deck playing, Deck incoming)
        {
            double target = System.Math.Floor(incoming.Beat) + playing.Beat % 1.0 + 0.01;
            incoming.Source.timeSamples = (int)(target * 60.0 / incoming.Track.bpm * incoming.Source.clip.frequency);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator BuildAndPerformASet()
        {
            var game = Object.FindAnyObjectByType<GameDirector>();
            var panel = Object.FindAnyObjectByType<InstructionPanel>();
            var builder = Object.FindAnyObjectByType<SetBuilderPanel>(FindObjectsInactive.Include);
            var performer = Object.FindAnyObjectByType<SetPerformer>();
            var board = Object.FindAnyObjectByType<DJBoard>();

            panel.actionButtons[1].SimulatePress(); // BUILD A SET
            panel.actionButtons[1].SimulatePress(); // CLUB
            Assert.AreEqual(GameMode.Build, game.Mode);
            Assert.IsTrue(builder.gameObject.activeSelf);
            Assert.IsFalse(panel.gameObject.activeSelf);

            builder.clear.SimulatePress();
            builder.goLive.SimulatePress();
            Assert.AreEqual(GameMode.Build, game.Mode, "can't go live with an empty set");

            builder.styleButtons[(int)TransitionStyle.Blend].SimulatePress();
            builder.crateButtons[0].SimulatePress();                          // Neon Tides, 128
            builder.crateButtons[1].SimulatePress();                          // Velvet Static, 124, blended in
            builder.styleButtons[(int)TransitionStyle.Cut].SimulatePress();
            builder.crateButtons[3].SimulatePress();                          // Sundial, 122, cut in
            CollectionAssert.AreEqual(new[] { TransitionStyle.Blend, TransitionStyle.Cut }, builder.Set.transitions);

            builder.goLive.SimulatePress();
            Assert.AreEqual(GameMode.Live, game.Mode);
            Assert.IsTrue(panel.gameObject.activeSelf);
            var A = board.A; var B = board.B;
            yield return WaitFor(() => A.deck.IsPlaying && B.deck.IsLoaded, 20f, "song 1 playing, song 2 loaded");
            Assert.AreEqual(0, performer.SongIndex);
            Assert.IsTrue(B.play.Highlighted, "first step of a blend: start the next song");

            // ---- Blend into song 2 ----
            B.play.SimulatePress();
            MatchTempo(B, A.deck.BPM);
            yield return null;
            LineUp(A.deck, B.deck);
            B.volume.SetValue(1f);
            board.crossfader.SetValue(0.5f);
            yield return new WaitForSeconds(6f);                              // both songs in sync for a while
            board.crossfader.SetValue(1f);
            yield return WaitFor(() => performer.SongIndex == 1, 3f, "blend complete");
            Assert.AreEqual(SetPerformer.Grade.Perfect, performer.Results[0].grade, performer.Results[0].note);

            // ---- Quick cut into song 3, now loading on Deck A ----
            yield return WaitFor(() => A.deck.IsLoaded && A.deck.TrackIndex == 3, 10f, "song 3 loaded on deck A");
            Assert.AreEqual(0f, A.volume.value, "the freed deck is reset");
            MatchTempo(A, B.deck.BPM);
            A.volume.SetValue(1f);
            yield return null;
            // Wait until just after a bar line on song 2, then PLAY and slam the crossfader together.
            yield return WaitFor(() => { double bar = B.deck.Beat / 4; return bar % 1.0 < 0.03; }, 6f, "bar line");
            A.play.SimulatePress();
            board.crossfader.SetValue(0f);
            yield return WaitFor(() => performer.SongIndex == 2, 3f, "cut complete");
            Assert.AreEqual(SetPerformer.Grade.Perfect, performer.Results[1].grade, performer.Results[1].note);
            Assert.IsTrue(performer.InFinale);

            panel.actionButtons[2].SimulatePress(); // END SET
            Assert.AreEqual(GameMode.LiveResults, game.Mode);
            Assert.AreEqual(3, SetPerformer.Stars(performer.Results));
        }
    }
}
