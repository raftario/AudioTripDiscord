using System;
using BepInEx;
using BepInEx.Configuration;
using Discord;
using UnityEngine.SceneManagement;

namespace AudioTripDiscord
{
    internal class PluginConfig
    {
        public readonly string HubLabel;
        public readonly string PlayingFormat;
        public readonly string PlayingLabel;
        public readonly bool RelativeTime;

        public PluginConfig(ConfigFile config)
        {
            RelativeTime = config.Bind("Format", "RelativeTime", false,
                "Display elapsed time since starting the song instead of elapsed time since starting the game").Value;
            HubLabel = config.Bind("Format", "HubLabel", "Hub", "Label to use when in the Hub World").Value;
            PlayingLabel = config.Bind("Format", "PlayingLabel", "Playing", "Label to use when playing a song").Value;
            PlayingFormat = config.Bind("Format", "PlayingFormat", "[%d] %a - %t",
                    "Format to use when displaying the current song (`%t` for song title, `%a` for song artist, `%l` for song length, `%A` for choreo author, `%d` for difficulty)")
                .Value;
        }
    }

    [BepInPlugin("discord", "Discord Presence", "0.1.0")]
    [BepInProcess("AudioTrip.exe")]
    public class Plugin : BaseUnityPlugin
    {
        private const long ClientId = 674393620832583692;

        private readonly long _initialTimestamp = CurrentTimestamp;
        private ActivityManager _activityManager;
        private Discord.Discord _discord;
        private PluginConfig _pluginConfig;

        private static long CurrentTimestamp =>
            (long) DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

        private Activity BaseActivity => new Activity
        {
            Timestamps = new ActivityTimestamps
            {
                Start = _pluginConfig.RelativeTime ? CurrentTimestamp : _initialTimestamp
            },
            Assets = new ActivityAssets
            {
                LargeImage = "icon"
            }
        };

        private void Awake()
        {
            Logger.LogDebug("Awake");

            _pluginConfig = new PluginConfig(Config);

            _discord = new Discord.Discord(ClientId, (long) CreateFlags.Default);
            _activityManager = _discord.GetActivityManager();

            var activity = BaseActivity;
            _activityManager.UpdateActivity(activity, UpdateActivityCallback);

            SceneManager.activeSceneChanged += ActiveSceneChanged;
        }

        private void Update()
        {
            _discord.RunCallbacks();
        }

        private void OnDestroy()
        {
            Logger.LogDebug("OnDestroy");

            SceneManager.activeSceneChanged -= ActiveSceneChanged;
        }

        private void ActiveSceneChanged(Scene _, Scene scene)
        {
            Logger.LogDebug($"ActiveSceneChanged({scene.name})");

            if (scene.name == "Hub World")
            {
                var activity = BaseActivity;
                activity.State = _pluginConfig.HubLabel;
                _discord.ActivityManagerInstance.UpdateActivity(activity, UpdateActivityCallback);
            }
            else if (scene.name != "Splash")
            {
                var songInfo = GlobalStorage.CurrentSongInfo;
                var choreoSet = songInfo.Choreographies[GlobalStorage.CurrentChoreoSet];

                var activity = BaseActivity;
                activity.State = _pluginConfig.PlayingLabel;
                activity.Details = _pluginConfig.PlayingFormat.Replace("%t", songInfo.title)
                    .Replace("%a", songInfo.artist)
                    .Replace("%l", TimeSpan.FromSeconds(songInfo.SongFullLengthInSeconds).ToString(@"mm\:ss"))
                    .Replace("%A", songInfo.authorID.displayName).Replace("%d", choreoSet.name);
                _discord.ActivityManagerInstance.UpdateActivity(activity, UpdateActivityCallback);
            }
        }

        private void UpdateActivityCallback(Result result)
        {
            if (result != Result.Ok) Logger.LogWarning("Couldn't update Discord activity");
        }
    }
}
