using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.IO;
using UnityEngine;

namespace BoomBoxOverhaul
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "henreh.boomboxoverhaul";
        public const string ModName = "BoomBoxOverhaulV2";
        public const string ModVersion = "2.0.1";

        internal static Plugin Instance;
        internal static Harmony Harmony;

        internal static ConfigEntry<bool> InfiniteBattery;
        internal static ConfigEntry<bool> KeepPlayingPocketed;
        internal static ConfigEntry<float> VolumeStep;
        internal static ConfigEntry<float> DefaultVolume;
        internal static ConfigEntry<KeyCode> OpenUiKey;
        internal static ConfigEntry<KeyCode> VolumeUpKey;
        internal static ConfigEntry<KeyCode> VolumeDownKey;
        internal static ConfigEntry<int> MaxCacheFiles;
        internal static ConfigEntry<float> ReadyTimeoutSeconds;
        internal static ConfigEntry<bool> AutoplayPlaylist;
        internal static ConfigEntry<bool> ShufflePlaylist;
        internal static ConfigEntry<int> MaxTrackSeconds;
        internal static ConfigEntry<bool> DeleteCacheOnBoot;

        internal static ConfigEntry<bool> AutoDownloadYtDlp;
        internal static ConfigEntry<bool> AutoDownloadFfmpeg;
        internal static ConfigEntry<string> FfmpegZipUrl;
        internal static ConfigEntry<bool> SearchPathForTools;

        internal static string PluginFolder = "";
        internal static string CacheFolder = "";
        internal static string ToolsFolder = "";

        private void Awake()
        {
            Instance = this;

            InfiniteBattery = Config.Bind("Gameplay", "InfiniteBattery", true, "Boombox does not require battery.");
            KeepPlayingPocketed = Config.Bind("Gameplay", "KeepPlayingPocketed", true, "Boombox keeps playing when pocketed.");
            VolumeStep = Config.Bind("Audio", "VolumeStep", 0.1f, "Volume increment/decrement amount.");
            DefaultVolume = Config.Bind("Audio", "DefaultVolume", 1.0f, "Default local boombox volume.");
            OpenUiKey = Config.Bind("Input", "OpenUiKey", KeyCode.B, "Open URL input UI.");
            VolumeUpKey = Config.Bind("Input", "VolumeUpKey", KeyCode.Equals, "Increase boombox volume.");
            VolumeDownKey = Config.Bind("Input", "VolumeDownKey", KeyCode.Minus, "Decrease boombox volume.");
            MaxCacheFiles = Config.Bind("Cache", "MaxCacheFiles", 15, "Maximum amount of downloaded tracks to keep.");
            ReadyTimeoutSeconds = Config.Bind("Networking", "ReadyTimeoutSeconds", 20f, "How long the server waits for clients to prepare before starting anyway.");
            AutoplayPlaylist = Config.Bind("Playlist", "AutoplayPlaylist", true, "Automatically continue to next playlist track.");
            ShufflePlaylist = Config.Bind("Playlist", "ShufflePlaylist", false, "Shuffle playlist order after resolving entries.");
            MaxTrackSeconds = Config.Bind("Downloads", "MaxTrackSeconds", 1800, "Maximum allowed track duration in seconds.");
            DeleteCacheOnBoot = Config.Bind("Cache", "DeleteCacheOnBoot", false, "Clear cache when the plugin loads.");

            AutoDownloadYtDlp = Config.Bind("Dependencies", "AutoDownloadYtDlp", true, "Automatically download yt-dlp if it is missing.");
            AutoDownloadFfmpeg = Config.Bind("Dependencies", "AutoDownloadFfmpeg", true, "Automatically download ffmpeg if it is missing.");
            FfmpegZipUrl = Config.Bind("Dependencies", "FfmpegZipUrl", "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip", "FFmpeg ZIP URL.");
            SearchPathForTools = Config.Bind("Dependencies", "SearchPathForTools", true, "Search PATH for yt-dlp and ffmpeg.");

            PluginFolder = Path.GetDirectoryName(Info.Location) ?? Paths.PluginPath;
            ToolsFolder = Path.Combine(PluginFolder, "tools");
            CacheFolder = Path.Combine(PluginFolder, "cache");

            Directory.CreateDirectory(ToolsFolder);
            Directory.CreateDirectory(CacheFolder);

            if (DeleteCacheOnBoot.Value)
            {
                FileSystemHelpers.TryDeleteDirectoryContents(CacheFolder);
            }

            Harmony = new Harmony(ModGuid);
            Harmony.PatchAll();

            Logger.LogInfo(ModName + " " + ModVersion + " loaded.");

            DependencyBootstrapper.EnsureStarted(this);
            Logger.LogInfo("Dependency bootstrap started.");

            GameObject netBoot = new GameObject("BoomBoxOverhaulNetBoot");
            netBoot.hideFlags = HideFlags.HideAndDontSave;
            netBoot.AddComponent<BoomBoxOverhaulNetBoot>();
            DontDestroyOnLoad(netBoot);

            Logger.LogInfo("BoomBoxOverhaul network boot started.");
        }

        internal static void Log(string msg)
        {
            if (Instance != null)
            {
                Instance.Logger.LogInfo(msg);
            }
        }

        internal static void Warn(string msg)
        {
            if (Instance != null)
            {
                Instance.Logger.LogWarning(msg);
            }
        }

        internal static void Error(string msg)
        {
            if (Instance != null)
            {
                Instance.Logger.LogError(msg);
            }
        }
    }
}
