using UnityEngine;

namespace DJMaster
{
    // Drives a venue's lasers, moving-head beams, LED panels and fireworks from the beat and crowd energy.
    public class LightShow : MonoBehaviour
    {
        public Transform[] lasers;          // long thin quads pivoted at the emitter
        public Renderer[] laserRenderers;
        public Transform[] beams;           // cones pivoted at the fixture
        public Renderer[] beamRenderers;
        public Renderer[] pulsers;          // LED walls, strips: flash on the beat
        public Light[] lights;
        public Transform spinner;           // mirror ball
        public ParticleSystem[] fireworks;
        public Color[] palette =
        {
            new Color(1f, 0.1f, 0.6f), new Color(0.1f, 0.6f, 1f), new Color(0.6f, 0.1f, 1f), new Color(0.1f, 1f, 0.5f),
        };

        Quaternion[] laserRest, beamRest;
        float[] lightBase;
        MaterialPropertyBlock block;
        float flash;

        void Awake()
        {
            laserRest = Rests(lasers);
            beamRest = Rests(beams);
            lightBase = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) lightBase[i] = lights[i].intensity;
        }

        void OnEnable() => BeatBus.Drop += OnDrop;
        void OnDisable() => BeatBus.Drop -= OnDrop;

        static Quaternion[] Rests(Transform[] ts)
        {
            var r = new Quaternion[ts.Length];
            for (int i = 0; i < ts.Length; i++) r[i] = ts[i].localRotation;
            return r;
        }

        void OnDrop()
        {
            flash = 1f;
            foreach (var fw in fireworks) if (fw != null) fw.Emit(60);
        }

        void Update()
        {
            block ??= new MaterialPropertyBlock();
            float e = BeatBus.Playing ? BeatBus.Energy * Mathf.Clamp01(BeatBus.Loudness * 2f) : 0f;
            float beat = (float)BeatBus.Beat;
            float pulse = BeatBus.Pulse;
            flash = Mathf.MoveTowards(flash, 0, Time.deltaTime * 2.5f);
            int bar = Mathf.FloorToInt(beat / 4f);
            Color main = palette[(bar / 4) % palette.Length];
            Color alt = palette[(bar / 4 + 1) % palette.Length];

            // Lasers fan out across the crowd; only once the energy is up.
            for (int i = 0; i < lasers.Length; i++)
            {
                float side = (i % 2 == 0 ? 1 : -1);
                float yaw = Mathf.Sin(beat * Mathf.PI * 0.25f + i * 0.7f) * 35f * side;
                float pitch = Mathf.Sin(beat * Mathf.PI * 0.125f + i) * 8f;
                lasers[i].localRotation = laserRest[i] * Quaternion.Euler(pitch, yaw, 0);
                float on = e > 0.45f ? Mathf.Clamp01((e - 0.45f) * 4f) : 0f;
                var c = (i % 2 == 0 ? main : alt) * (0.4f + 0.6f * pulse);
                c.a = on;
                block.SetColor("_Color", c);
                laserRenderers[i].SetPropertyBlock(block);
                laserRenderers[i].enabled = on > 0.01f;
            }

            for (int i = 0; i < beams.Length; i++)
            {
                float t = Time.time * (0.3f + e * 0.6f) + i * 1.3f;
                beams[i].localRotation = beamRest[i] * Quaternion.Euler(Mathf.Sin(t) * 20f, Mathf.Cos(t * 0.8f) * 25f, 0);
                var c = (i % 2 == 0 ? alt : main);
                c.a = 0.05f + 0.12f * e + 0.15f * pulse * e;
                block.SetColor("_Color", c);
                beamRenderers[i].SetPropertyBlock(block);
            }

            for (int i = 0; i < pulsers.Length; i++)
            {
                float k = 0.15f + 0.85f * Mathf.Max(pulse * (0.3f + e), flash);
                var c = Color.Lerp(main, alt, (i % 3) / 2f) * k;
                c.a = 1;
                block.SetColor("_Color", c);
                pulsers[i].SetPropertyBlock(block);
            }

            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].color = i % 2 == 0 ? main : alt;
                lights[i].intensity = lightBase[i] * (0.3f + 0.7f * pulse * (0.4f + e) + flash);
            }

            if (spinner != null) spinner.Rotate(0, Time.deltaTime * 25f, 0, Space.Self);
        }
    }
}
