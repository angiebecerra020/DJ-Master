using UnityEngine;

namespace DJMaster
{
    // One mixer channel: 3-band isolator EQ, a sweep filter (left = low-pass, right = high-pass) and gain.
    // Sits on the deck's AudioSource and processes its output on the audio thread.
    public class ChannelStrip : MonoBehaviour
    {
        [Range(0, 1)] public float low = 0.5f;
        [Range(0, 1)] public float mid = 0.5f;
        [Range(0, 1)] public float high = 0.5f;
        [Range(0, 1)] public float filter = 0.5f;
        public float gain;          // final linear gain, written by Mixer

        public float Level { get; private set; } // smoothed output peak, for meters

        const double LowCross = 250, HighCross = 3200;
        readonly Biquad[] lowBand = { new(), new() };
        readonly Biquad[] highBand = { new(), new() };
        readonly Biquad[] filt = { new(), new() };
        int sampleRate;
        float lastGain, lastL = 1, lastM = 1, lastH = 1, lastFilter = -1;
        volatile float peak;

        void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            for (int c = 0; c < 2; c++)
            {
                lowBand[c].SetLowpass(sampleRate, LowCross, 0.707);
                highBand[c].SetHighpass(sampleRate, HighCross, 0.707);
            }
        }

        void Update()
        {
            float p = peak;
            Level = Mathf.Max(p, Level - Time.deltaTime * 1.5f);
            peak = 0;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (sampleRate == 0) return;
            float gL = DJMath.EqGain(low), gM = DJMath.EqGain(mid), gH = DJMath.EqGain(high);
            float g = gain, f = filter;

            bool filterOn = Mathf.Abs(f - 0.5f) > 0.03f;
            if (filterOn && Mathf.Abs(f - lastFilter) > 0.002f)
            {
                lastFilter = f;
                for (int c = 0; c < 2; c++)
                {
                    if (f < 0.5f) filt[c].SetLowpass(sampleRate, 20000 * Mathf.Pow(150f / 20000f, (0.5f - f) * 2f), 1.4);
                    else filt[c].SetHighpass(sampleRate, 20 * Mathf.Pow(6000f / 20f, (f - 0.5f) * 2f), 1.4);
                }
            }
            else if (!filterOn) lastFilter = -1;

            int frames = data.Length / channels;
            float inv = frames > 0 ? 1f / frames : 0;
            float pk = 0;
            for (int i = 0; i < frames; i++)
            {
                float u = i * inv; // ramp parameters across the buffer to avoid zipper noise
                float gi = lastGain + (g - lastGain) * u;
                float li = lastL + (gL - lastL) * u, mi = lastM + (gM - lastM) * u, hi = lastH + (gH - lastH) * u;
                for (int c = 0; c < channels; c++)
                {
                    int k = i * channels + c;
                    int ch = c < 2 ? c : 1;
                    float x = data[k];
                    float lo = lowBand[ch].Process(x);
                    float h = highBand[ch].Process(x);
                    float y = lo * li + (x - lo - h) * mi + h * hi;
                    if (filterOn) y = filt[ch].Process(y);
                    y *= gi;
                    data[k] = y;
                    float a = y < 0 ? -y : y;
                    if (a > pk) pk = a;
                }
            }
            lastGain = g; lastL = gL; lastM = gM; lastH = gH;
            if (pk > peak) peak = pk;
        }
    }
}
