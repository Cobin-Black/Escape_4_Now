// Plays the title screen audio and the announcement for the game mode the player starts.

using System.Collections;
using Escape4Now.Menus;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Escape4Now.Audio
{
    //Lives in the MainScreenAudio prefab in Assets/Resources, where each clip is assigned.
    //It stays alive between scenes so an announcement keeps playing while the gamemode's screen loads.
    [RequireComponent(typeof(AudioSource))]
    public sealed class MainScreenAudio : MonoBehaviour
    {
        private const string PrefabName = "MainScreenAudio";

        [Header("Title Screen")]
        [SerializeField] private AudioClip titleScreen;
        [SerializeField, Min(0f), Tooltip("Seconds to wait after the title screen loads.")]
        private float titleDelay = 1f;
        [Header("Game Mode Select")]
        [SerializeField] private AudioClip raceSelect;
        [SerializeField] private AudioClip survivalSelect;
        [SerializeField] private AudioClip kingOfTheHillSelect;
        [SerializeField] private AudioClip hotPotatoSelect;
        [Header("Volume")]
        [SerializeField, Range(0f, 1f), Tooltip("Volume of these clips. The Sound effects setting still applies on top.")]
        private float volume = 1f;

        private static MainScreenAudio instance;
        private static bool titlePlayed;
        private AudioSource source;

        //Starts fresh when Play mode starts, even if the editor skips reloading scripts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedState()
        {
            instance = null;
            titlePlayed = false;
        }

        //Loads the clips early so they start without a delay.
        public static void Prepare() => GetInstance();

        //Called by the title screen. Plays once each time the game is started.
        public static void PlayTitleScreen()
        {
            if (titlePlayed) return;
            MainScreenAudio player = GetInstance();
            if (player == null) return;
            titlePlayed = true;
            player.StartCoroutine(player.PlayTitleAfterDelay());
        }

        //Called by the main menu. Modes use the main menu's order: Race, Survival, King of the Hill, Hot Potato.
        public static void PlayModeSelect(int mode)
        {
            MainScreenAudio player = GetInstance();
            if (player == null) return;
            player.StopAllCoroutines();
            switch (mode)
            {
                case 0: player.Play(player.raceSelect); break;
                case 1: player.Play(player.survivalSelect); break;
                case 2: player.Play(player.kingOfTheHillSelect); break;
                case 3: player.Play(player.hotPotatoSelect); break;
            }
        }

        private static MainScreenAudio GetInstance()
        {
            if (instance != null) return instance;
            var prefab = Resources.Load<MainScreenAudio>(PrefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"[Audio] The {PrefabName} prefab is missing from Assets/Resources.");
                return null;
            }
            instance = Instantiate(prefab);
            instance.name = PrefabName;
            DontDestroyOnLoad(instance.gameObject);
            return instance;
        }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            //Use the saved Sound effects setting right away. The main menu only applies it once it opens.
            AudioListener.volume = PlayerPrefs.GetInt(MainMenuScreen.SfxKey, MainMenuScreen.DefaultSfx) / 8f;
            foreach (AudioClip clip in new[] { titleScreen, raceSelect, survivalSelect, kingOfTheHillSelect, hotPotatoSelect })
                if (clip != null) clip.LoadAudioData();
        }

        //Skips the title audio if the player already left the title screen.
        private IEnumerator PlayTitleAfterDelay()
        {
            Scene titleScene = SceneManager.GetActiveScene();
            yield return new WaitForSecondsRealtime(titleDelay);
            if (SceneManager.GetActiveScene() == titleScene) Play(titleScreen);
        }

        //A new clip replaces the one that is playing, so they never talk over each other.
        private void Play(AudioClip clip)
        {
            source.Stop();
            if (clip == null)
            {
                Debug.LogWarning($"[Audio] A clip is not assigned on the {PrefabName} prefab.", this);
                return;
            }
            source.clip = clip;
            source.volume = volume;
            source.Play();
        }
    }
}
