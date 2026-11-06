using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DJMaster.Tests
{
    // Loads the real scene and drives the board with a simulated fingertip.
    public class BoardPlayTests
    {
        Pointer finger;

        [UnitySetUp]
        public IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/DJMaster.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
            yield return null;
            finger = Pointers.Create();
            finger.Active = false;
        }

        [TearDown]
        public void Cleanup() => Pointers.Remove(finger);

        IEnumerator Poke(PokeButton b)
        {
            finger.DirectPress = false;
            finger.Active = true;
            finger.Position = b.transform.TransformPoint(new Vector3(0, 0.015f, 0));
            yield return null; yield return null;
            finger.Position = b.transform.TransformPoint(new Vector3(0, -0.01f, 0));
            yield return null; yield return null;
            finger.Position = b.transform.TransformPoint(new Vector3(0, 0.03f, 0));
            yield return null;
            finger.Active = false;
            yield return null;
        }

        IEnumerator Drag(Fader f, float from, float to)
        {
            finger.DirectPress = false;
            Vector3 Along(float v) => f.horizontal ? new Vector3((v - 0.5f) * f.length, 0.005f, 0) : new Vector3(0, 0.005f, (v - 0.5f) * f.length);
            finger.Active = true;
            for (int i = 0; i <= 20; i++)
            {
                finger.Position = f.transform.TransformPoint(Along(Mathf.Lerp(from, to, i / 20f)));
                yield return null;
            }
            finger.Active = false;
            yield return null;
        }

        IEnumerator WaitFor(System.Func<bool> cond, float seconds)
        {
            float t = 0;
            while (!cond() && t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator PokingPlayStartsTheDeck()
        {
            var board = Object.FindAnyObjectByType<DJBoard>();
            Assert.IsNotNull(board);
            var deck = board.A.deck;
            yield return WaitFor(() => deck.IsLoaded, 20f);
            Assert.IsTrue(deck.IsLoaded, "track should finish rendering");
            Assert.IsFalse(deck.IsPlaying);

            yield return Poke(board.A.play);
            Assert.IsTrue(deck.IsPlaying, "a poke from above should press PLAY");

            // Sliding a finger in from the side must not press it.
            finger.Active = true;
            finger.Position = board.A.play.transform.TransformPoint(new Vector3(0.05f, -0.01f, 0));
            yield return null;
            finger.Position = board.A.play.transform.TransformPoint(new Vector3(0f, -0.01f, 0));
            yield return null; yield return null;
            finger.Active = false;
            yield return null;
            Assert.IsTrue(deck.IsPlaying, "brushing in from the side should not toggle PLAY");
        }

        [UnityTest]
        public IEnumerator FaderFollowsFingerAndDrivesMixer()
        {
            var board = Object.FindAnyObjectByType<DJBoard>();
            var mixer = board.mixer;
            Assert.AreEqual(0f, board.A.volume.value, 1e-4f);
            yield return Drag(board.A.volume, 0f, 1f);
            Assert.Greater(board.A.volume.value, 0.9f);
            Assert.Greater(mixer.channelFader[0], 0.9f);

            yield return Drag(board.crossfader, 0.5f, 0f);
            Assert.Less(board.crossfader.value, 0.1f);
        }

        [UnityTest]
        public IEnumerator TempoFaderIsFineGrained()
        {
            var board = Object.FindAnyObjectByType<DJBoard>();
            var tempo = board.B.tempo;
            yield return Drag(tempo, 0.5f, 1f);
            // Finger moved half the slot; with the 0.35 drag ratio the cap moves much less.
            Assert.Greater(tempo.value, 0.6f);
            Assert.Less(tempo.value, 0.75f);
        }

        [UnityTest]
        public IEnumerator MenuLeadsThroughVenueToTheColorQuiz()
        {
            var game = Object.FindAnyObjectByType<GameDirector>();
            var lessons = Object.FindAnyObjectByType<LessonDirector>();
            var panel = Object.FindAnyObjectByType<InstructionPanel>();
            var env = Object.FindAnyObjectByType<EnvironmentManager>();
            var board = Object.FindAnyObjectByType<DJBoard>();
            Assert.AreEqual(GameMode.Title, game.Mode);

            yield return Poke(panel.actionButtons[0]); // LEARN
            Assert.AreEqual(GameMode.ChooseVenue, game.Mode);
            yield return new WaitForSeconds(0.3f);
            yield return Poke(panel.actionButtons[2]); // FESTIVAL
            Assert.AreEqual(GameMode.Learn, game.Mode);
            Assert.AreEqual(EnvMode.Festival, env.Mode);
            Assert.AreEqual(0, lessons.StepIndex);

            // Wrong color: told what it was, no progress.
            board.B.low.SimulateTouch();
            yield return null;
            StringAssert.Contains("PINK", lessons.Feedback);
            Assert.AreEqual(0, lessons.StepIndex);

            board.A.low.SimulateTouch();
            yield return WaitFor(() => lessons.StepIndex == 1, 4f);
            Assert.AreEqual(1, lessons.StepIndex);
        }

        [UnityTest]
        public IEnumerator EnvironmentButtonsSwitchVenues()
        {
            var env = Object.FindAnyObjectByType<EnvironmentManager>();
            env.clubButton.SimulatePress();
            yield return null;
            Assert.AreEqual(EnvMode.Club, env.Mode);
            Assert.IsTrue(env.club.root.activeSelf);
            Assert.IsFalse(env.festival.root.activeSelf);
            env.festivalButton.SimulatePress();
            yield return null;
            Assert.IsTrue(env.festival.root.activeSelf);
            env.roomButton.SimulatePress();
            yield return null;
            Assert.IsFalse(env.club.root.activeSelf);
            Assert.AreEqual(CameraClearFlags.SolidColor, env.cam.clearFlags);
        }
    }
}
