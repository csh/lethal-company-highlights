using System;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using Steamworks;
using Steamworks.Data;

namespace LethalCompanyHighlights
{
    enum RecordingKind
    {
        Marker,
        Clip
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("ainavt.lc.lethalconfig")]
    [BepInDependency("com.elitemastereric.coroner")]
    public class SteamHighlightsPlugin : BaseUnityPlugin
    {
        internal new static ManualLogSource Logger;

        internal static ConfigEntry<bool> isEnabledConfigEntry;
        internal static ConfigEntry<bool> openOverlayConfigEntry;
        internal static ConfigEntry<int> overlayDelayConfigEntry;
        internal static ConfigEntry<int> preDeathDurationConfigEntry;
        internal static ConfigEntry<int> postDeathDurationConfigEntry;
        internal static ConfigEntry<RecordingKind> recordingKindConfigEntry;

#pragma warning disable IDE0051
        private void Awake()
        {
            Logger = base.Logger;

            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");


            isEnabledConfigEntry = Config.Bind<bool>(
                "General",
                "Enable",
                true,
                "Enable Steam to capture highlights."
            );

            openOverlayConfigEntry = Config.Bind<bool>(
                "General",
                "Open Overlay on Death",
                true,
                "Would you like to open the Steam Overlay upon death?"
            );
            
            overlayDelayConfigEntry = Config.Bind<int>(
                "General",
                "Overlay Delay",
                2,
                "Delay in seconds before the Steam Overlay will open if enabled.\nMust be between 2 and 60 seconds."
            );

            preDeathDurationConfigEntry = Config.Bind<int>(
                "Recording",
                "Context Duration",
                10,
                "Duration to record prior to death.\nMust be between 5 and 60 seconds."
            );

            postDeathDurationConfigEntry = Config.Bind<int>(
                "Recording",
                "Post-Death Duration",
                10,
                "Duration to record after death.\nMust be between 5 and 60 seconds."
            );

            recordingKindConfigEntry = Config.Bind<RecordingKind>(
                "General",
                "Recording Style",
                RecordingKind.Clip,
                "Choose the kind of recording to use.\n" +
                "Marker: Records a marker for the death event, has pretty icons you can click to jump to.\n" +
                "Duration not supported. \n" +
                "Clip: Marks a clip of the death event with duration support, easier for one-click sharing. No pretty icons :("
            );

            var enabledConfigToggle = new BoolCheckBoxConfigItem(isEnabledConfigEntry, new BoolCheckBoxOptions
            {
                Name = "Enable Death Capture",
                Description = "Enable Steam to capture highlights, you must have Steam running and have Game Recording enabled in the Steam settings.",
                Section = "General",
                RequiresRestart = false,
                CanModifyCallback = CanModifySettings
            });

            var openOverlayConfigToggle = new BoolCheckBoxConfigItem(openOverlayConfigEntry, new BoolCheckBoxOptions
            {
                Name = "Open Overlay on Death",
                Description = "Would you like to open the Steam Overlay upon death?",
                Section = "General",
                RequiresRestart = false,
                CanModifyCallback = CanModifySettings
            });

            var overlayDelaySlider = new IntSliderConfigItem(overlayDelayConfigEntry, new IntSliderOptions
            {
                Name = "Overlay Delay",
                Description = "Delay in seconds before the Steam Overlay will open if enabled.\nMust be between 2 and 60 seconds.",
                Section = "General",
                Min = 2,
                Max = 60,
                RequiresRestart = false,
                CanModifyCallback = CanModifyOverlaySettings
            });

            var recordingKindDropdown = new EnumDropDownConfigItem<RecordingKind>(recordingKindConfigEntry, new EnumDropDownOptions
            {
                Name = "Recording Style",
                Description = "Choose the kind of recording to use.\n" +
                              "Marker: Records a marker for the death event, has pretty icons you can click to jump to.\n" +
                              "Duration not supported. \n" +
                              "Clip: Marks a clip of the death event with duration support, easier for one-click sharing. No pretty icons :(",
                Section = "General",
                RequiresRestart = false,
                CanModifyCallback = CanModifySettings
            });

            var preDeathDurationSlider = new IntSliderConfigItem(preDeathDurationConfigEntry, new IntSliderOptions
            {
                Name = "Context Duration",
                Description = "Duration to record prior to death.\nMust be between 5 and 60 seconds.",
                Section = "Recording",
                Min = 5,
                Max = 60,
                RequiresRestart = false,
                CanModifyCallback = CanModifyDurationSettings
            });

            var postDeathDurationSlider = new IntSliderConfigItem(postDeathDurationConfigEntry, new IntSliderOptions
            {
                Name = "Post-Death Duration",
                Description = "Duration to record after death.\nMust be between 5 and 60 seconds.",
                Section = "Recording",
                Min = 5,
                Max = 60,
                RequiresRestart = false,
                CanModifyCallback = CanModifyDurationSettings
            });

            LethalConfigManager.AddConfigItem(enabledConfigToggle);
            LethalConfigManager.AddConfigItem(openOverlayConfigToggle);
            LethalConfigManager.AddConfigItem(overlayDelaySlider);
            LethalConfigManager.AddConfigItem(recordingKindDropdown);
            LethalConfigManager.AddConfigItem(preDeathDurationSlider);
            LethalConfigManager.AddConfigItem(postDeathDurationSlider);
            LethalConfigManager.SetModDescription("Death Capture");

            Harmony.CreateAndPatchAll(typeof(RoundPatches));
            Harmony.CreateAndPatchAll(typeof(PlayerPatches));
        }

        private CanModifyResult CanModifyOverlaySettings()
        {
            if (SteamUtils.IsOverlayEnabled && openOverlayConfigEntry.Value)
            {
                return CanModifyResult.True();
            }
            else
            {
                return CanModifyResult.False("Steam Overlay is disabled or you have not enabled the option to open the overlay on death.");
            }
        }

        private CanModifyResult CanModifyDurationSettings()
        {
            if (recordingKindConfigEntry.Value == RecordingKind.Marker)
            {
                return CanModifyResult.False("Duration settings are not applicable for Marker recordings.");
            }
            else if (recordingKindConfigEntry.Value == RecordingKind.Clip)
            {
                return CanModifyResult.True();
            }
            else
            {
                return CanModifyResult.False("Unknown recording kind selected.");
            }
        }

        private CanModifyResult CanModifySettings()
        {
            if (SteamClient.IsValid && SteamUtils.IsOverlayEnabled)
            {
                return CanModifyResult.True();
            }
            else
            {
                return CanModifyResult.False("Steam is not running or the Steam Overlay is disabled.");
            }
        }
    }

    class RoundPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(MenuManager), nameof(MenuManager.SetLoadingScreen))]
        static void LoadingPostfix(bool isLoading)
        {
            if (isLoading)
            {
                SteamTimeline.SetTimelineGameMode(TimelineGameMode.LoadingScreen);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SetMapScreenInfoToCurrentLevel))]
        static void SetMapScreenInfoToCurrentLevelPostfix()
        {
            var planetName = StartOfRound.Instance.currentLevel.PlanetName;
            SteamTimeline.SetTimelineGameMode(TimelineGameMode.Staging);
            SteamTimeline.SetTimelineTooltip($"Orbiting {planetName}", 0);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SceneManager_OnLoadComplete1))]
        static void OnLoadPostfix(string sceneName)
        {
            if (sceneName == "MainMenu" || sceneName == "SampleSceneRelay")
            {
                SteamTimeline.EndGamePhase();
                SteamTimeline.SetTimelineGameMode(TimelineGameMode.Menus);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.openingDoorsSequence))]
        public static void openingDoorsSequencePostfix()
        {
            var planetName = StartOfRound.Instance.currentLevel.PlanetName;
            SteamTimeline.SetTimelineGameMode(TimelineGameMode.Playing);
            SteamTimeline.StartGamePhase();
            SteamTimeline.SetTimelineTooltip($"Exploring {planetName}", -5);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.unloadSceneForAllPlayers))]
        static void unloadSceneForAllPlayersPostfix()
        {
            var planetName = StartOfRound.Instance.currentLevel.PlanetName;
            SteamTimeline.SetTimelineGameMode(TimelineGameMode.Staging);
            SteamTimeline.SetTimelineTooltip($"Orbiting {planetName}", 0);
            SteamTimeline.EndGamePhase();
        }
    }

    class PlayerPatches
    {
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayer))]
        [HarmonyPostfix]
        static void Postfix(PlayerControllerB __instance)
        {
            SteamHighlightsPlugin.Logger.LogDebug($"PLAYER KILLED: {__instance.playerUsername}");
            SaveDeathClip(__instance);
        }

        static void SaveDeathClip(PlayerControllerB player)
        {
            if (SteamHighlightsPlugin.isEnabledConfigEntry.Value == false) return;

            var cause = Coroner.API.GetCauseOfDeath(player);

            var causeOfDeath = cause.HasValue
                ? Coroner.API.StringifyCauseOfDeath(cause.Value, null)
                : "Unknown";

            TimelineEventHandle handle;
            if (SteamHighlightsPlugin.recordingKindConfigEntry.Value == RecordingKind.Marker)
            {
                handle = SteamTimeline.AddInstantaneousTimelineEvent(
                    "Death",
                    $"{player.playerUsername} died: {causeOfDeath}",
                    "steam_death",
                    1,
                    0,
                    TimelineEventClipPriority.Standard
                );
            }
            else
            {
                handle = SteamTimeline.StartRangeTimelineEvent(
                    "Death",
                    $"{player.playerUsername} died: {causeOfDeath}",
                    "steam_death",
                    1,
                    -SteamHighlightsPlugin.preDeathDurationConfigEntry.Value,
                    TimelineEventClipPriority.Featured
                );

                SteamTimeline.EndRangeTimelineEvent(handle, SteamHighlightsPlugin.postDeathDurationConfigEntry.Value);
            }

            if (SteamHighlightsPlugin.openOverlayConfigEntry.Value)
            {
                // Wait before opening the overlay to allow any surprise/shock to set in. 
                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(SteamHighlightsPlugin.overlayDelayConfigEntry.Value));
                    SteamTimeline.OpenOverlayToTimelineEvent(handle);
                });
            }
        }
    }
}