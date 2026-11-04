using System.Collections.Generic;
using UnityEngine;

namespace DJMaster
{
    // A dance floor of low-poly silhouettes drawn with GPU instancing. They bounce on the beat,
    // and the more energy the crowd has, the higher they jump and the more hands go up.
    public class Crowd : MonoBehaviour
    {
        public int count = 120;
        public Vector2 areaMin = new Vector2(-6, 2);   // local x/z rectangle
        public Vector2 areaMax = new Vector2(6, 13);
        public float floorY;
        public Material bodyMaterial;
        public Material glowMaterial;                  // glow sticks / phone lights
        [Range(0, 1)] public float glowChance = 0.15f;
        public int seed = 1;

        struct Person { public Vector3 pos; public float yaw, height, phase, hype, armSpread; public bool glow; }

        Person[] people;
        Mesh body, head, arm, stick;
        readonly List<Matrix4x4> bodies = new(), heads = new(), arms = new(), sticks = new();
        static readonly List<Matrix4x4> batch = new(1023);
        float bounce;

        void Awake()
        {
            body = MeshKit.Prism(8, 0.17f, 0.13f, 1f);
            head = MeshKit.Prism(6, 0.11f, 0.1f, 0.24f);
            arm = MeshKit.Prism(4, 0.035f, 0.03f, 0.6f);
            stick = MeshKit.Prism(4, 0.02f, 0.02f, 0.22f);

            var rng = new System.Random(seed);
            people = new Person[count];
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Lerp(areaMin.x, areaMax.x, (float)rng.NextDouble());
                float z = Mathf.Lerp(areaMin.y, areaMax.y, (float)rng.NextDouble());
                people[i] = new Person
                {
                    pos = new Vector3(x, floorY, z),
                    yaw = 180f + (float)(rng.NextDouble() - 0.5) * 50f, // mostly facing the DJ
                    height = 1.45f + (float)rng.NextDouble() * 0.35f,
                    phase = (float)rng.NextDouble(),
                    hype = (float)rng.NextDouble(),
                    armSpread = 10f + (float)rng.NextDouble() * 25f,
                    glow = rng.NextDouble() < glowChance,
                };
            }
        }

        void Update()
        {
            bodies.Clear(); heads.Clear(); arms.Clear(); sticks.Clear();
            float energy = BeatBus.Energy * Mathf.Clamp01(BeatBus.Loudness * 2f);
            bounce = BeatBus.Playing ? (float)(BeatBus.Beat % 1.0) : 0.5f;
            var root = transform.localToWorldMatrix;

            for (int i = 0; i < people.Length; i++)
            {
                ref var p = ref people[i];
                bool dancing = p.hype < 0.25f + energy;
                float beatPhase = (bounce + p.phase * 0.12f) % 1f;
                float jump = dancing ? Mathf.Pow(1f - beatPhase, 3f) * (0.04f + energy * energy * 0.22f) : 0f;
                float sway = Mathf.Sin(Time.time * 1.3f + p.phase * 6.28f) * (dancing ? 6f : 2f);

                var rot = Quaternion.Euler(0, p.yaw, sway);
                Vector3 basePos = p.pos + Vector3.up * jump;
                float torso = p.height - 0.3f;
                bodies.Add(root * Matrix4x4.TRS(basePos, rot, new Vector3(1, torso, 1)));
                Vector3 neck = basePos + rot * (Vector3.up * torso);
                heads.Add(root * Matrix4x4.TRS(neck + rot * (Vector3.up * 0.02f), rot, Vector3.one));

                bool handsUp = dancing && p.hype < energy * 0.9f;
                if (handsUp)
                {
                    float wave = Mathf.Sin((float)BeatBus.Beat * Mathf.PI + p.phase * 6f) * 8f;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3 shoulder = neck + rot * new Vector3(0.15f * s, -0.12f, 0);
                        var armRot = rot * Quaternion.Euler(0, 0, -s * (p.armSpread + wave));
                        arms.Add(root * Matrix4x4.TRS(shoulder, armRot, Vector3.one));
                        if (p.glow && s == 1) sticks.Add(root * Matrix4x4.TRS(shoulder + armRot * (Vector3.up * 0.6f), armRot, Vector3.one));
                    }
                }
                else if (p.glow)
                {
                    sticks.Add(root * Matrix4x4.TRS(neck + rot * new Vector3(0.22f, -0.5f, 0.1f), rot * Quaternion.Euler(-60, 0, 0), Vector3.one));
                }
            }

            Draw(body, bodyMaterial, bodies);
            Draw(head, bodyMaterial, heads);
            Draw(arm, bodyMaterial, arms);
            if (glowMaterial != null) Draw(stick, glowMaterial, sticks);
        }

        static void Draw(Mesh mesh, Material mat, List<Matrix4x4> all)
        {
            if (mat == null) return;
            for (int start = 0; start < all.Count; start += 1023)
            {
                batch.Clear();
                for (int i = start; i < Mathf.Min(all.Count, start + 1023); i++) batch.Add(all[i]);
                Graphics.DrawMeshInstanced(mesh, 0, mat, batch);
            }
        }
    }
}
