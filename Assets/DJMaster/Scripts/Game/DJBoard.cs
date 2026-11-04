using System;
using UnityEngine;

namespace DJMaster
{
    [Serializable]
    public class DeckControls
    {
        public Deck deck;
        public PokeButton play, cue, load, headphones;
        public Fader tempo, volume;
        public Knob high, mid, low, filter;
        public JogWheel jog;
    }

    // Connects the physical controls to the decks and mixer, and keeps button lights in sync.
    public class DJBoard : MonoBehaviour
    {
        public Mixer mixer;
        public DeckControls[] channels = new DeckControls[2];
        public Fader crossfader;
        public PokeButton tap, airHorn;

        public DJControl[] AllControls { get; private set; }

        public DeckControls A => channels[0];
        public DeckControls B => channels[1];

        void Awake()
        {
            AllControls = GetComponentsInChildren<DJControl>(true);
            for (int i = 0; i < channels.Length; i++)
            {
                var ch = channels[i];
                int idx = i;
                ch.play.Pressed += ch.deck.TogglePlay;
                ch.cue.Pressed += ch.deck.Cue;
                ch.load.Pressed += () => ch.deck.LoadNext(channels[1 - idx].deck.TrackIndex);
                ch.headphones.Pressed += () => mixer.headphoneCue[idx] = !mixer.headphoneCue[idx];
            }
            airHorn.Pressed += Sfx.AirHorn;
        }

        void Update()
        {
            for (int i = 0; i < channels.Length; i++)
            {
                var ch = channels[i];
                ch.deck.tempoFader = ch.tempo.value;
                mixer.channelFader[i] = ch.volume.value;
                ch.deck.Strip.high = ch.high.value;
                ch.deck.Strip.mid = ch.mid.value;
                ch.deck.Strip.low = ch.low.value;
                ch.deck.Strip.filter = ch.filter.value;

                ch.play.Lit = ch.deck.IsPlaying && (ch.deck.Beat % 1.0) < 0.5; // blinks on the beat
                ch.cue.Lit = ch.deck.IsLoaded && !ch.deck.IsPlaying;
                ch.load.Lit = ch.deck.IsLoading && Time.time % 0.4f < 0.2f;
                ch.headphones.Lit = mixer.headphoneCue[i];
            }
            mixer.crossfader = crossfader.value;
            int dom = mixer.DominantDeck;
            tap.Lit = dom >= 0 && (channels[dom].deck.Beat % 1.0) < 0.15;
        }

        public DJControl Find(string id)
        {
            foreach (var c in AllControls) if (c.id == id) return c;
            return null;
        }

        public void ClearHighlights()
        {
            foreach (var c in AllControls) c.Highlighted = false;
        }

        // Put every control back to a known state: faders down, EQs centered, tempo at 0%.
        public void ResetControls()
        {
            foreach (var ch in channels)
            {
                ch.tempo.SetValue(0.5f);
                ch.volume.SetValue(0f);
                ch.high.SetValue(0.5f); ch.mid.SetValue(0.5f); ch.low.SetValue(0.5f); ch.filter.SetValue(0.5f);
            }
            crossfader.SetValue(0.5f);
            mixer.headphoneCue[0] = mixer.headphoneCue[1] = false;
        }
    }
}
