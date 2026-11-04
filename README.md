# DJ MASTER

Learn to DJ with just your hands. A mixed reality DJ school for Meta Quest.

A full DJ controller appears on your real desk: two decks, a mixer, jog wheels, EQs, filters and a crossfader.
A step-by-step lesson takes you from pressing PLAY to your first beatmatched transition in about 6 minutes.
Then play a 3-minute gig and keep the crowd moving. When you're ready, swap your room for a packed club or a
night-time festival main stage with lasers, a mirror ball, fireworks and a crowd that reacts to your mixing.

No controllers needed. Every interaction is a fingertip poke, slide or spin.

| Your room (mixed reality) | Club | Festival |
|---|---|---|
| ![room](Docs/room.png) | ![club](Docs/club.png) | ![festival](Docs/festival.png) |

![board](Docs/board.png)

*Editor renders. On Quest, the "room" view is your real room through passthrough.*

## What you learn

DJ School is one guided session of 16 steps. The control you need next pulses gold, and a hint appears if you're stuck.

| # | Skill | What you do |
|---|---|---|
| 1-3 | Decks and faders | Press PLAY, raise the channel fader, crossfade to Deck A |
| 4 | Feel the beat | Tap the TAP pad on four kicks in a row |
| 5 | Phrasing | Tap exactly on a 4-bar line (bar 1, 5, 9, 13) |
| 6-8 | Cueing | LOAD Deck B, preview it in the headphones (PHONES), press PLAY |
| 9 | Beatmatch: tempo | Ride Deck B's TEMPO fader until both BPMs match |
| 10 | Beatmatch: phase | Nudge Deck B's jog wheel until the SYNC light turns green |
| 11-14 | The blend | Cut the bass, bring Deck B in, swap the basslines, finish the transition |
| 15-16 | Effects | Filter sweep and the air horn |

**Gig mode** (3 minutes) scores your set. The crowd's energy rises for in-sync blends, finished transitions and bass drops
(cut the LOW, slam it back on a bar line). It falls for trainwrecks (two tracks out of sync), muddy bass clashes, silence,
and playing one track for too long. Live tips show what's going wrong.

**Free play** has no timer.

## Hand controls

| Control | Gesture |
|---|---|
| Buttons | Poke down from above. Brushing past from the side won't trigger them. |
| Faders | Touch the cap and slide. The tempo fader is geared 1:3 for fine control and clicks into 0%. |
| Knobs | Touch the top and slide your finger away/right to turn up, toward you/left to turn down. Double-tap to reset to center. |
| Jog wheels | Rest a fingertip on the platter and spin: clockwise speeds the track up, counter-clockwise slows it. When paused, it scrubs. |

A glowing dot on each index fingertip shows exactly where you're touching, and turns gold while you're holding a control.
The platters turn once per bar with their white marker pointing forward on beat 1, so two in-phase tracks have matching markers.

**Scene panel** (to the left of the board): MY ROOM / CLUB / FESTIVAL, RECENTER, and BOARD UP / DOWN for comfort.

## Designed for the hackathon guidelines

- **Hands-first:** everything works with hands alone. Nothing needs a controller.
- **Seated, airplane-seat friendly:** the board snaps in front of you at desk height, with every control inside arm's reach. RECENTER and height buttons are always available.
- **Bus-stop test:** cold start lands you on the menu with tracks preloading. One lesson is about 6 minutes and a gig is 3. Taking the headset off or opening the system menu pauses audio and time, and the game resumes where you left off.
- **Original, not a wrapper:** all eight tracks are synthesized at runtime by `TrackSynth` (house, techno and disco loops, arranged in DJ phrases). There is no licensed music, no streaming, and no third-party services.

## Run it in the editor (no headset)

1. Open the project in **Unity 6000.6.4f1** and open `Assets/Scenes/DJMaster.unity`.
2. Press Play. The mouse acts as a fingertip:
   - **Left click / drag:** press buttons, drag faders, turn knobs (drag up/right), spin the jog wheels
   - **Scroll wheel:** turn the knob under the cursor
   - **Right drag:** look around

To regenerate the scene after changing the builder: **DJ Master > Build Scene**.

## Build to Quest

1. **Unity Hub > Installs > 6000.6.4f1 > Add modules > Android Build Support** (include OpenJDK and Android SDK & NDK).
2. **DJ Master > Configure Player Settings for Quest** (IL2CPP, ARM64, min API 32, package `com.djmaster.vr`, hand tracking + passthrough).
3. **File > Build Profiles > Android > Switch Platform**.
4. **Edit > Project Settings > XR Plug-in Management > Android tab:** check **OpenXR**, then under OpenXR enable the **Meta XR** feature group. Run **Meta > Tools > Project Setup Tool** and click **Fix All**.
5. Put the Quest in developer mode, connect over USB, then **Build And Run**.

**For the competition:** upload the APK to a release channel named **Competition** in the Meta Developer Dashboard and create an Invite URL for the judges.

## Tests

- **EditMode (15):** mixer math (constant-power crossfader, EQ curve, tempo range, phase wrap), synth loop length/levels/kick timing, text wrap, gig stars.
- **PlayMode (6):** loads the real scene and drives it with a simulated fingertip. Covers poking PLAY (and *not* triggering it from the side), fader drags, the fine-geared tempo fader, venue switching, the lesson advancing, and a full DJ School run from step 1 to "LESSON COMPLETE".

Run them from **Window > General > Test Runner**.

## Project map

```
Assets/DJMaster/
  Scripts/Audio/        TrackSynth (procedural music), TrackLibrary, Deck, ChannelStrip (EQ/filter DSP), Mixer, Sfx, DJMath
  Scripts/Interaction/  Pointer, HandPointerSource (index fingertip), MousePointerSource, PokeButton, Fader, Knob, JogWheel
  Scripts/Game/         GameDirector (menu/gig/free), LessonDirector (DJ School), MixJudge (crowd energy + score), DJBoard
  Scripts/UI/           DeckDisplay, SyncMeter, InstructionPanel
  Scripts/World/        EnvironmentManager (passthrough/club/festival), SeatedAnchor, Crowd (GPU-instanced), LightShow, MeshKit
  Shaders/              Additive + Glow (single-pass stereo safe)
  Editor/SceneBuilder.cs  builds the entire scene from primitives
```

Built with the Meta XR Core SDK v207 (OVRCameraRig, OVRHand/OVRSkeleton with OpenXR hand skeletons, OVRPassthroughLayer).
