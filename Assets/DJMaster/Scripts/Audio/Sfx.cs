using UnityEngine;

namespace DJMaster
{
    // Interface and crowd sounds, synthesized at startup (no audio assets needed).
    public class Sfx : MonoBehaviour
    {
        public AudioSource uiSource;
        public AudioSource crowdSource;      // cheers and the air horn
        public AudioSource ambienceSource;   // looping crowd murmur, volume follows crowd energy

        static Sfx instance;
        AudioClip click, success, fail, cheer, horn, murmur;
        const int SR = 44100;

        void Awake()
        {
            instance = this;
            click = Make("click", 0.03f, t => Mathf.Sin(2 * Mathf.PI * 2200 * t) * Mathf.Exp(-t * 180) * 0.5f);
            success = Make("success", 0.5f, t =>
            {
                float f = t < 0.12f ? 660 : t < 0.24f ? 880 : 1320;
                return Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-(t % 0.12f) * 9) * 0.35f * (1 - t / 0.5f);
            });
            fail = Make("fail", 0.35f, t => Mathf.Sign(Mathf.Sin(2 * Mathf.PI * (180 - t * 120) * t)) * 0.12f * (1 - t / 0.35f));
            cheer = MakeCheer();
            horn = MakeHorn();
            murmur = MakeMurmur();
            if (ambienceSource != null)
            {
                ambienceSource.clip = murmur;
                ambienceSource.loop = true;
                ambienceSource.volume = 0;
                ambienceSource.Play();
            }
        }

        public static void Click() => Play(instance?.click, 0.6f, ui: true);
        public static void Success() => Play(instance?.success, 0.7f, ui: true);
        public static void Fail() => Play(instance?.fail, 0.6f, ui: true);
        public static void Cheer(float volume = 1f) => Play(instance?.cheer, volume, ui: false);
        public static void AirHorn() => Play(instance?.horn, 0.8f, ui: false);

        public static void SetAmbience(float volume)
        {
            if (instance == null || instance.ambienceSource == null) return;
            var s = instance.ambienceSource;
            s.volume = Mathf.MoveTowards(s.volume, volume, Time.deltaTime * 0.5f);
        }

        static void Play(AudioClip clip, float volume, bool ui)
        {
            if (instance == null || clip == null) return;
            var src = ui ? instance.uiSource : instance.crowdSource;
            if (src != null) src.PlayOneShot(clip, volume);
        }

        static AudioClip Make(string name, float seconds, System.Func<float, float> fn)
        {
            int n = (int)(seconds * SR);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = fn((float)i / SR);
            var c = AudioClip.Create(name, n, 1, SR, false);
            c.SetData(data, 0);
            return c;
        }

        // Filtered noise swell plus scattered claps and "woo" whistles.
        static AudioClip MakeCheer()
        {
            int n = (int)(2.6f * SR);
            var d = new float[n * 2];
            var rng = new System.Random(7);
            float l = 0, r = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float env = Mathf.Min(1, t / 0.15f) * Mathf.Exp(-Mathf.Max(0, t - 0.5f) * 1.6f);
                l += 0.18f * ((float)(rng.NextDouble() * 2 - 1) - l);
                r += 0.18f * ((float)(rng.NextDouble() * 2 - 1) - r);
                d[i * 2] = l * env * 0.9f;
                d[i * 2 + 1] = r * env * 0.9f;
            }
            for (int k = 0; k < 90; k++) // claps
            {
                int at = (int)(rng.NextDouble() * 2.0 * SR);
                int ch = rng.Next(2);
                float g = (float)(0.1 + rng.NextDouble() * 0.2) * Mathf.Exp(-at / (float)SR * 0.8f);
                for (int i = 0; i < 900 && at + i < n; i++)
                    d[(at + i) * 2 + ch] += (float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-i / 120f) * g;
            }
            for (int k = 0; k < 6; k++) // whistles
            {
                int at = (int)(rng.NextDouble() * 1.2 * SR);
                float f0 = 900 + (float)rng.NextDouble() * 500;
                int len = (int)(0.5f * SR);
                int ch = rng.Next(2);
                for (int i = 0; i < len && at + i < n; i++)
                {
                    float t = (float)i / SR;
                    float f = f0 + 400 * Mathf.Sin(t * 5);
                    d[(at + i) * 2 + ch] += Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Sin(Mathf.PI * i / len) * 0.06f;
                }
            }
            var c = AudioClip.Create("cheer", n, 2, SR, false);
            c.SetData(d, 0);
            return c;
        }

        // The classic stadium air horn: detuned saw chord in three blasts.
        static AudioClip MakeHorn()
        {
            int n = (int)(1.5f * SR);
            var d = new float[n];
            float[] freqs = { 370, 466, 554, 740 };
            double[] phase = new double[freqs.Length];
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float blast = t < 0.18f ? 1 : t < 0.24f ? 0 : t < 0.42f ? 1 : t < 0.48f ? 0 : t < 1.4f ? 1 : 0;
                float x = 0;
                for (int k = 0; k < freqs.Length; k++)
                {
                    phase[k] += freqs[k] * (1 + 0.004f * Mathf.Sin(t * 33 + k)) / SR;
                    x += (float)(phase[k] % 1) * 2 - 1;
                }
                lp += 0.3f * (x * 0.25f - lp);
                d[i] = Mathf.Clamp(lp * 2.2f, -1, 1) * blast * 0.5f;
            }
            var c = AudioClip.Create("airhorn", n, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        static AudioClip MakeMurmur()
        {
            int n = 4 * SR;
            var d = new float[n * 2];
            var rng = new System.Random(11);
            float l = 0, r = 0;
            for (int i = 0; i < n; i++)
            {
                l += 0.05f * ((float)(rng.NextDouble() * 2 - 1) - l);
                r += 0.05f * ((float)(rng.NextDouble() * 2 - 1) - r);
                float edge = Mathf.Min(1, Mathf.Min(i, n - i) / 2000f); // fade the loop seam
                d[i * 2] = l * 1.6f * edge;
                d[i * 2 + 1] = r * 1.6f * edge;
            }
            var c = AudioClip.Create("murmur", n, 2, SR, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
