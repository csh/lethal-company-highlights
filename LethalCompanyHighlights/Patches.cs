using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GameNetcodeStuff;
using HarmonyLib;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace LethalCompanyHighlights;

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
        PlayerPatches.playersKilled.Clear();
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
    internal static readonly IDictionary<ulong, float> lastSeen = new Dictionary<ulong, float>();
    internal static readonly ISet<string> playersKilled = new HashSet<string>();

    [HarmonyPostfix, HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.Start))]
    static void StartPostfix(PlayerControllerB __instance)
    {
        if (__instance.IsLocalPlayer == false) return;
        SteamHighlightsPlugin.Logger.LogDebug("Attaching visibility tracker to local player");

        if (!__instance.gameObject.TryGetComponent<VisibilityTracker>(out _))
        {
            __instance.gameObject.AddComponent<VisibilityTracker>().Initialize(lastSeen);
        }
    }

    internal static void RemoveFromLastSeen(ulong clientId)
    {
        if (lastSeen.Remove(clientId))
        {
            SteamHighlightsPlugin.Logger.LogDebug("Removed a client from lastSeen tracker");
        }
    }

    [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ReviveDeadPlayers))]
    static void ReviveDeadPlayersPostfix()
    {
        SteamHighlightsPlugin.Logger.LogDebug("ReviveDeadPlayers called, clearing killed players list.");
        playersKilled.Clear();
        lastSeen.Clear();
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayer))]
    [HarmonyPostfix]
    static void KillPlayerPostfix(PlayerControllerB __instance)
    {
        SteamHighlightsPlugin.Logger.LogDebug($"Player '{__instance.playerUsername}' died");
        if (SteamHighlightsPlugin.isEnabledConfigEntry.Value == false) return;
        SteamHighlightsPlugin.Instance.StartCoroutine(SaveDeathClip(__instance));
    }

    static bool IsPlayerNearby(PlayerControllerB targetPlayer, float radius)
    {
        Vector3 originPos = StartOfRound.Instance.localPlayerController.transform.position;

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

    private static bool HasSeenRecently(PlayerControllerB target, float window)
    {
        return lastSeen.TryGetValue(target.playerClientId, out float seenTime) && (Time.time - seenTime) <= window;
    }

    static IEnumerator SaveDeathClip(PlayerControllerB player)
    {
        if (playersKilled.Add(player.playerUsername) == false)
        {
            SteamHighlightsPlugin.Logger.LogWarning($"Player '{player.playerUsername}' has already been recorded as dead.");
            yield break;
        }

        SteamTimeline.AddGamePhaseTag(player.playerUsername, "steam_death", "Died", 50);

        PlayerControllerB localPlayer = StartOfRound.Instance.localPlayerController;

        /**
         * Least to most expensive checks, hopefully.
         *
         * 1. Check if the local player died
         * 2. Check if the local player observed the death in spectator mode
         * 3. Check if the local player is alive and the deceased is in close proximity
         * 4. Check if the local player is alive and has recently had line of sight on the deceased
         *
         * This should probably be adequate to prevent capture of deaths taking place on the other side of the map.
         */
        if (player != localPlayer)
        {
            if (!(localPlayer.isPlayerDead && localPlayer.spectatedPlayerScript == player))
            {
                if (!(localPlayer.isPlayerDead == false && IsPlayerNearby(player, 8f)))
                {
                    if (!(localPlayer.isPlayerDead == false && HasSeenRecently(player, 10f)))
                    {
                        yield break;
                    }
                }
            }
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