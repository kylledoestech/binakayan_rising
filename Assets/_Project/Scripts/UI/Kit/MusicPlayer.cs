using UnityEngine;

namespace BinakayanRising.UI.Kit
{
    /// <summary>
    /// Background music: one looping track at a time, crossfaded when the screen changes, at the
    /// Music level from Settings (#49).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tracks load from <c>Resources/Music/</c> by name, so nothing has to be assigned in the
    /// Inspector, and a missing file is silence rather than an error. All are CC0; titles,
    /// authors and sources are in <c>Docs/CREDITS.md</c>.
    /// </para>
    /// <para>
    /// Two sources trade places for the crossfade. A third plays the short win and loss stings
    /// over the music, which ducks under them. The level follows
    /// <see cref="UserPrefs.EffectiveMusicVolume"/> live, through <see cref="UserPrefs.AudioChanged"/>,
    /// so dragging the slider is heard while it moves.
    /// </para>
    /// </remarks>
    public sealed class MusicPlayer : MonoBehaviour
    {
        /// <summary>What is playing.</summary>
        public enum Track
        {
            None = 0,

            /// <summary>The splash and title screen.</summary>
            Menu = 1,

            /// <summary>The encampment and every panel opened from it.</summary>
            Camp = 2,

            /// <summary>Deployment and the battle replay.</summary>
            Battle = 3
        }

        private const float FadeSeconds = 1.6f;
        private const float DuckLevel = 0.25f;
        private const float StingVolume = 0.9f;

        private static MusicPlayer instance;

        private readonly AudioSource[] loops = new AudioSource[2];
        private AudioSource sting;
        private int active;
        private Track current;
        private float level;
        private float duckUntil;

        /// <summary>The track playing, or fading in.</summary>
        public static Track Current
        {
            get { return instance != null ? instance.current : Track.None; }
        }

        /// <summary>The clip of the track playing, for the screenshot audit.</summary>
        public static AudioClip CurrentClip
        {
            get { return instance != null ? instance.loops[instance.active].clip : null; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        /// <summary>The Resources path of a track's clip, or null for none.</summary>
        public static string PathOf(Track track)
        {
            switch (track)
            {
                case Track.Menu: return "Music/music_menu";
                case Track.Camp: return "Music/music_camp";
                case Track.Battle: return "Music/music_battle";
                default: return null;
            }
        }

        /// <summary>Crossfades to <paramref name="track"/>. Asking for what already plays does nothing.</summary>
        public static void Play(Track track)
        {
            Ensure().Switch(track);
        }

        /// <summary>Plays the short win or loss sting over the music, ducking it for the length of the sting.</summary>
        public static void Sting(bool won)
        {
            Ensure().PlaySting(won ? "Sfx/sting_victory" : "Sfx/sting_defeat");
        }

        private static MusicPlayer Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            // Not under any screen: music has to outlive every screen, and the battle scene load.
            var go = new GameObject("Music") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MusicPlayer>();
            for (int i = 0; i < instance.loops.Length; i++)
            {
                instance.loops[i] = NewSource(go, true);
            }

            instance.sting = NewSource(go, false);
            instance.level = UserPrefs.EffectiveMusicVolume;
            UserPrefs.AudioChanged += instance.OnAudioChanged;
            return instance;
        }

        private static AudioSource NewSource(GameObject host, bool loop)
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.bypassEffects = true;
            source.bypassReverbZones = true;
            source.priority = 0;
            source.volume = 0f;
            return source;
        }

        private void OnAudioChanged()
        {
            level = UserPrefs.EffectiveMusicVolume;
        }

        private void Switch(Track track)
        {
            if (track == current)
            {
                return;
            }

            current = track;
            string path = PathOf(track);
            AudioClip clip = path != null ? Resources.Load<AudioClip>(path) : null;

            // The outgoing source keeps playing while it fades; the other one takes the new clip.
            active = 1 - active;
            AudioSource incoming = loops[active];
            incoming.Stop();
            incoming.clip = clip;
            incoming.volume = 0f;
            if (clip != null)
            {
                incoming.Play();
            }
        }

        private void PlaySting(string path)
        {
            AudioClip clip = Resources.Load<AudioClip>(path);
            if (clip == null || level <= 0f)
            {
                return;
            }

            sting.PlayOneShot(clip, StingVolume * level);
            duckUntil = Time.unscaledTime + clip.length + 0.3f;
        }

        private void Update()
        {
            float target = level * (Time.unscaledTime < duckUntil ? DuckLevel : 1f);
            float step = Time.unscaledDeltaTime / FadeSeconds;
            for (int i = 0; i < loops.Length; i++)
            {
                AudioSource source = loops[i];
                float goal = i == active ? target : 0f;
                source.volume = Mathf.MoveTowards(source.volume, goal, step * Mathf.Max(level, 0.05f));

                // A slider dragged down must be heard at once, not faded toward.
                if (source.volume > goal && i == active)
                {
                    source.volume = goal;
                }

                if (i != active && source.isPlaying && source.volume <= 0f)
                {
                    source.Stop();
                }
            }
        }

        private void OnDestroy()
        {
            UserPrefs.AudioChanged -= OnAudioChanged;
            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
