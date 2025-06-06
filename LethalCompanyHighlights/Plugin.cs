using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
using UnityEngine;

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
        internal static SteamHighlightsPlugin Instance { get; private set; }
        internal new static ManualLogSource Logger;

        internal static ConfigEntry<bool> isEnabledConfigEntry;
        internal static ConfigEntry<bool> openOverlayConfigEntry;
        internal static ConfigEntry<bool> onlyMyDeathsConfigEntry;
        internal static ConfigEntry<int> overlayDelayConfigEntry;
        internal static ConfigEntry<int> preDeathDurationConfigEntry;
        internal static ConfigEntry<int> postDeathDurationConfigEntry;
        internal static ConfigEntry<RecordingKind> recordingKindConfigEntry;

        private Harmony harmony;

#pragma warning disable IDE0051
        private void Awake()
        {
            Instance = this;
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

            onlyMyDeathsConfigEntry = Config.Bind<bool>(
                "General",
                "My Deaths Only",
                true,
                "Would you like to open the overlay for all deaths, or just yours?"
            );

            overlayDelayConfigEntry = Config.Bind<int>(
                "General",
                "Overlay Delay",
                5,
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
                Section = "Overlay Settings",
                RequiresRestart = false,
                CanModifyCallback = CanModifySettings
            });

            var onlyMyDeathsConfigToggle = new BoolCheckBoxConfigItem(onlyMyDeathsConfigEntry, new BoolCheckBoxOptions
            {
                Name = "My Deaths Only",
                Description = "Would you like to open the overlay for all deaths, or just yours?",
                Section = "Overlay Settings",
                RequiresRestart = false,
                CanModifyCallback = CanModifyOverlaySettings
            });

            var overlayDelaySlider = new IntSliderConfigItem(overlayDelayConfigEntry, new IntSliderOptions
            {
                Name = "Overlay Delay",
                Description = "Delay in seconds before the Steam Overlay will open if enabled.\nMust be between 2 and 60 seconds.",
                Section = "Overlay Settings",
                Min = 2,
                Max = 60,
                RequiresRestart = false,
                CanModifyCallback = CanModifyOverlaySettings
            });

            var recordingKindDropdown = new EnumDropDownConfigItem<RecordingKind>(recordingKindConfigEntry, new EnumDropDownOptions
            {
                Name = "Recording Style",
                Description = "Choose the kind of recording to use.\n\n" +
                              "Marker: Records a marker for the death event, has pretty icons you can click to jump to.\n" +
                              "Duration not supported. \n\n" +
                              "Clip: Marks a clip of the death event with duration support, easier for one-click sharing.\n" +
                              "No pretty icons :(",
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

            LethalConfigManager.SkipAutoGen();
            LethalConfigManager.AddConfigItem(enabledConfigToggle);
            LethalConfigManager.AddConfigItem(openOverlayConfigToggle);
            LethalConfigManager.AddConfigItem(onlyMyDeathsConfigToggle);
            LethalConfigManager.AddConfigItem(overlayDelaySlider);
            LethalConfigManager.AddConfigItem(recordingKindDropdown);
            LethalConfigManager.AddConfigItem(preDeathDurationSlider);
            LethalConfigManager.AddConfigItem(postDeathDurationSlider);

            harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll(typeof(RoundPatches));
            harmony.PatchAll(typeof(PlayerPatches));
        }

        private void OnDestroy()
        {
            if (isEnabledConfigEntry.Value)
            {
                SteamTimeline.EndGamePhase();
            }
            harmony.UnpatchSelf();
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
        static readonly Regex RemoveLeadingNumber = new(@"^\d+\s+", RegexOptions.Compiled);
        static string currentPhaseId = null;

        [HarmonyPostfix, HarmonyPatch(typeof(MenuManager), nameof(MenuManager.SetLoadingScreen))]
        static void SetLoadingScreenPostfix(bool isLoading)
        {
            if (isLoading)
            {
                SteamHighlightsPlugin.Logger.LogDebug("MenuManager::SetLoadingScreen, 'LoadingScreen' phase");
                SteamTimeline.SetTimelineGameMode(TimelineGameMode.LoadingScreen);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SetMapScreenInfoToCurrentLevel))]
        static void SetMapScreenInfoToCurrentLevelPostfix()
        {
            SteamHighlightsPlugin.Logger.LogDebug("StartOfRound::SetMapScreenInfoToCurrentLevel, 'Orbiting' phase");
            var planetName = RemoveLeadingNumber.Replace(StartOfRound.Instance.currentLevel.PlanetName, "");
            SteamTimeline.SetTimelineGameMode(TimelineGameMode.Staging);
            SteamTimeline.SetTimelineTooltip($"Orbiting {planetName}", -3);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.openingDoorsSequence))]
        static void OpeningDoorsSequencePostfix()
        {
            SteamHighlightsPlugin.Logger.LogDebug("StartOfRound::openingDoorsSequence, 'Exploring' phase");

            var planetName = RemoveLeadingNumber.Replace(StartOfRound.Instance.currentLevel.PlanetName, "");
            currentPhaseId = $"{planetName}-{Guid.NewGuid()}";

            SteamTimeline.SetTimelineGameMode(TimelineGameMode.Playing);
            SteamTimeline.ClearTimelineTooltip(-2);
            SteamTimeline.StartGamePhase();
            SteamTimeline.SetGamePhaseId(currentPhaseId);
            SteamTimeline.AddGamePhaseTag(planetName, "steam_marker", "Planet", 100);
            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                if (!player.isActiveAndEnabled) continue;
                SteamTimeline.AddGamePhaseTag(player.playerUsername, "steam_group", "Players", 75);
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(MenuManager), nameof(MenuManager.Start))]
        static void StartPostfix()
        {
            SteamTimeline.EndGamePhase();
            SteamTimeline.SetTimelineGameMode(TimelineGameMode.Menus);
            PlayerPatches._playersKilled.Clear();
            currentPhaseId = null;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ShipLeave))]
        static void ShipLeavePostfix()
        {
            SteamTimeline.EndGamePhase();
            currentPhaseId = null;
        }
    }

    class PlayerPatches
    {
        internal static ISet<string> _playersKilled = new HashSet<string>();

        [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ReviveDeadPlayers))]
        static void ReviveDeadPlayersPostfix()
        {
            SteamHighlightsPlugin.Logger.LogDebug("ReviveDeadPlayers called, clearing killed players list.");
            _playersKilled.Clear();
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayer))]
        [HarmonyPostfix]
        static void KillPlayerPostfix(PlayerControllerB __instance)
        {
            SteamHighlightsPlugin.Logger.LogDebug($"Player '{__instance.playerUsername}' died");
            if (SteamHighlightsPlugin.isEnabledConfigEntry.Value == false) return;
            SteamHighlightsPlugin.Instance.StartCoroutine(SaveDeathClip(__instance));
        }

        static bool CanSeePlayer(PlayerControllerB from, PlayerControllerB to, float maxDistance)
        {
            Vector3 eyeOrigin = from.gameplayCamera.transform.position;
            Vector3 targetPos = to.transform.position + Vector3.up * 0.5f;

            if (Vector3.Distance(eyeOrigin, targetPos) > maxDistance)
                return false;

            return !Physics.Linecast(eyeOrigin, targetPos, StartOfRound.Instance.collidersAndRoomMaskAndPlayers);
        }

        static bool IsPlayerNearby(PlayerControllerB originPlayer, PlayerControllerB targetPlayer, float radius)
        {
            Vector3 originPos = originPlayer.transform.position;

            float distSqr = (originPos - targetPlayer.transform.position).sqrMagnitude;
            if (distSqr > radius * radius)
                return false;

            Collider[] hits = Physics.OverlapSphere(originPos, radius, StartOfRound.Instance.playersMask);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<PlayerControllerB>(out var player) && player == targetPlayer)
                {
                    return true;
                }
            }
            return false;
        }

        static IEnumerator SaveDeathClip(PlayerControllerB player)
        {
            if (_playersKilled.Add(player.playerUsername) == false)
            {
                SteamHighlightsPlugin.Logger.LogWarning($"Player '{player.playerUsername}' has already been recorded as dead.");
                yield break;
            }


            SteamTimeline.AddGamePhaseTag(player.playerUsername, "steam_death", "Died", 50);

            PlayerControllerB localPlayer = StartOfRound.Instance.localPlayerController;
            bool isLocalPlayer = player == localPlayer;
            bool isLocalAlive = !localPlayer.isPlayerDead;
            bool isNearby = IsPlayerNearby(localPlayer, player, 15f);
            bool isVisible = isNearby || CanSeePlayer(localPlayer, player, 25f);
            bool isSpectated = localPlayer.isPlayerDead && localPlayer.spectatedPlayerScript == player;

            bool shouldSaveClip =
                isLocalPlayer ||                               // Local player died
                (isLocalAlive && (isNearby || isVisible)) ||   // Local player was nearby or saw it happen
                isSpectated;                                   // Local player was spectating the victim

            if (!shouldSaveClip)
            {
                yield break;
            }

            SteamHighlightsPlugin.Logger.LogDebug($"Recording death clip for player: '{player.playerUsername}'");
            
            var cause = Coroner.API.GetCauseOfDeath(player);

            var causeOfDeath = cause.HasValue
                ? Coroner.API.StringifyCauseOfDeath(cause.Value, null)
                : "Unknown cause of death.";

            var priority = StartOfRound.Instance.localPlayerController == player ? 100u : 90u;
            var clipName = $"{player.playerUsername} died";

            TimelineEventHandle handle;
            if (SteamHighlightsPlugin.recordingKindConfigEntry.Value == RecordingKind.Marker)
            {
                handle = SteamTimeline.AddInstantaneousTimelineEvent(
                    clipName,
                    causeOfDeath,
                    "steam_death",
                    priority,
                    0,
                    TimelineEventClipPriority.Featured
                );
            }
            else
            {
                handle = SteamTimeline.AddRangeTimelineEvent(
                    clipName,
                    causeOfDeath,
                    "steam_death",
                    priority,
                    -SteamHighlightsPlugin.preDeathDurationConfigEntry.Value,
                    SteamHighlightsPlugin.preDeathDurationConfigEntry.Value + SteamHighlightsPlugin.postDeathDurationConfigEntry.Value,
                    TimelineEventClipPriority.Featured
                );
            }

            if (SteamHighlightsPlugin.openOverlayConfigEntry.Value)
            {
                if (SteamHighlightsPlugin.onlyMyDeathsConfigEntry.Value && player != StartOfRound.Instance.localPlayerController)
                {
                    yield break;
                }

                yield return new WaitForSecondsRealtime(SteamHighlightsPlugin.overlayDelayConfigEntry.Value);
                SteamTimeline.OpenOverlayToTimelineEvent(handle);
            }
        }
    }
}