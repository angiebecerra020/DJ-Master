using System;
using UnityEngine;
using UnityEngine.XR;

namespace DJMaster
{
    public enum EnvMode { Room, Club, Festival }

    // Switches between mixed reality (your real room via passthrough) and the two virtual venues.
    public class EnvironmentManager : MonoBehaviour
    {
        [Serializable]
        public class Look
        {
            public GameObject root;
            public Material skybox;
            public Color ambient = new Color(0.3f, 0.3f, 0.35f);
            public Color fogColor = Color.black;
            public float fogDensity;
            public float keyLight = 0.6f;
        }

        public Camera cam;
        [Tooltip("Soft directional light so the board reads well in every venue.")]
        public Light keyLight;
        public OVRPassthroughLayer passthrough;
        [Tooltip("Grey stand-in room shown in the editor, where there is no passthrough camera feed.")]
        public GameObject editorRoomStandIn;
        public Look room, club, festival;
        public PokeButton roomButton, clubButton, festivalButton;

        public EnvMode Mode { get; private set; }
        public event Action<EnvMode> Changed;

        void Start() => SetMode((EnvMode)PlayerPrefs.GetInt("env", (int)EnvMode.Room));

        public void SetMode(EnvMode mode)
        {
            Mode = mode;
            PlayerPrefs.SetInt("env", (int)mode);

            bool mr = mode == EnvMode.Room;
            if (passthrough != null) passthrough.hidden = !mr;
            if (editorRoomStandIn != null) editorRoomStandIn.SetActive(mr && !XRSettings.isDeviceActive);

            var look = mode switch { EnvMode.Club => club, EnvMode.Festival => festival, _ => room };
            foreach (var l in new[] { room, club, festival })
                if (l.root != null) l.root.SetActive(l == look);

            // Passthrough shows through wherever the eye buffer is transparent.
            cam.clearFlags = mr || look.skybox == null ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            cam.backgroundColor = mr ? new Color(0, 0, 0, 0) : Color.black;
            RenderSettings.skybox = look.skybox;
            RenderSettings.ambientLight = look.ambient;
            RenderSettings.fog = look.fogDensity > 0;
            RenderSettings.fogColor = look.fogColor;
            RenderSettings.fogDensity = look.fogDensity;
            if (keyLight != null) keyLight.intensity = look.keyLight;

            if (roomButton != null) roomButton.Lit = mode == EnvMode.Room;
            if (clubButton != null) clubButton.Lit = mode == EnvMode.Club;
            if (festivalButton != null) festivalButton.Lit = mode == EnvMode.Festival;

            if (!mr && BeatBus.Playing) Sfx.Cheer(0.5f);
            Changed?.Invoke(mode);
        }
    }
}
