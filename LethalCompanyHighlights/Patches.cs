using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using GameNetcodeStuff;
using HarmonyLib;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace LethalCompanyHighlights;

internal static class SimpleDeathTracker
{
    private static readonly ISet<ulong> PlayersKilled = new HashSet<ulong>();

    internal static bool PlayerKilled(PlayerControllerB player)
    {
        return PlayersKilled.Add(player.playerClientId);
    }

    internal static void Reset()
    {
        PlayersKilled.Clear();
    }
}

internal class RoundPatches
{
    private static readonly Regex RemoveLeadingNumber = new(@"^\d+\s+", RegexOptions.Compiled);
    private static string _currentPhaseId;

    [HarmonyPostfix, HarmonyPatch(typeof(MenuManager), nameof(MenuManager.SetLoadingScreen))]
    private static void SetLoadingScreenPostfix(bool isLoading)
    {
        if (!isLoading) return;
        SteamHighlightsPlugin.Logger.LogDebug("MenuManager::SetLoadingScreen, 'LoadingScreen' phase");
        SteamTimeline.SetTimelineGameMode(TimelineGameMode.LoadingScreen);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SetMapScreenInfoToCurrentLevel))]
    private static void SetMapScreenInfoToCurrentLevelPostfix()
    {
        SteamHighlightsPlugin.Logger.LogDebug("StartOfRound::SetMapScreenInfoToCurrentLevel, 'Orbiting' phase");
        var planetName = RemoveLeadingNumber.Replace(StartOfRound.Instance.currentLevel.PlanetName, "");
        SteamTimeline.SetTimelineGameMode(TimelineGameMode.Staging);
        SteamTimeline.SetTimelineTooltip($"Orbiting {planetName}", -3);
    }

    [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.openingDoorsSequence))]
    private static void OpeningDoorsSequencePostfix()
    {
        SteamHighlightsPlugin.Logger.LogDebug("StartOfRound::openingDoorsSequence, 'Exploring' phase");

        var planetName = RemoveLeadingNumber.Replace(StartOfRound.Instance.currentLevel.PlanetName, "");
        _currentPhaseId = $"{planetName}-{Guid.NewGuid()}";

        SteamTimeline.SetTimelineGameMode(TimelineGameMode.Playing);
        SteamTimeline.ClearTimelineTooltip(-2);
        SteamTimeline.StartGamePhase();
        SteamTimeline.SetGamePhaseId(_currentPhaseId);
        SteamTimeline.AddGamePhaseTag(planetName, "steam_marker", "Planet", 100);
        foreach (var player in StartOfRound.Instance.allPlayerScripts)
        {
            if (!player.isActiveAndEnabled) continue;
            SteamTimeline.AddGamePhaseTag(player.playerUsername, "steam_group", "Players", 75);
        }

        if (!StartOfRound.Instance.localPlayerController.gameObject.TryGetComponent<VisibilityTracker>(out var tracker))
        {
            tracker = StartOfRound.Instance.localPlayerController.gameObject.AddComponent<VisibilityTracker>();
        }
        tracker.Initialize();
    }

    [HarmonyPostfix, HarmonyPatch(typeof(MenuManager), nameof(MenuManager.Start))]
    private static void StartPostfix()
    {
        if (GameNetworkManager.Instance.disableSteam) return;
        SteamTimeline.EndGamePhase();
        SteamTimeline.SetTimelineGameMode(TimelineGameMode.Menus);
        SimpleDeathTracker.Reset();
        _currentPhaseId = null;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ShipLeave))]
    private static void ShipLeavePostfix()
    {
        if (VisibilityTracker.Instance)
        {
            VisibilityTracker.Instance.StopVisibilityCoroutine();
        }
        SteamTimeline.EndGamePhase();
        _currentPhaseId = null;
    }
}

internal class PlayerPatches
{
    [HarmonyPostfix, HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ReviveDeadPlayers))]
    private static void ReviveDeadPlayersPostfix()
    {
        SteamHighlightsPlugin.Logger.LogDebug("ReviveDeadPlayers called, clearing killed players list.");
        SimpleDeathTracker.Reset();
    }

    [HarmonyPostfix, HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayer))]
    private static void KillPlayerPostfix([SuppressMessage("ReSharper", "InconsistentNaming")] PlayerControllerB __instance)
    {
        SteamHighlightsPlugin.Instance.StartCoroutine(SaveDeathClip(__instance));
    }

    [HarmonyPostfix, HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayerClientRpc))]
    private static void KillPlayerClientRpcPostfix(int playerId)
    {
        var player = StartOfRound.Instance.allPlayerScripts[playerId];
        if (player && player.isActiveAndEnabled)
        {
            SteamHighlightsPlugin.Instance.StartCoroutine(SaveDeathClip(player));
        }
    }

    private static bool IsPlayerNearby(PlayerControllerB targetPlayer, float radius)
    {
        var localPos = StartOfRound.Instance.localPlayerController.transform.position;
        return (localPos - targetPlayer.transform.position).sqrMagnitude <= radius * radius;
    }

    private static IEnumerator SaveDeathClip(PlayerControllerB player)
    {
        if (SteamHighlightsPlugin.IsEnabledConfigEntry.Value == false) yield break;
        
        if (SimpleDeathTracker.PlayerKilled(player) == false)
        {
            SteamHighlightsPlugin.Logger.LogDebug($"Player '{player.playerUsername}' has already been recorded as dead.");
            yield break;
        }

        SteamHighlightsPlugin.Logger.LogDebug($"Player '{player.playerUsername}' died");

        if (GameNetworkManager.Instance.disableSteam == false)
        {
            SteamTimeline.AddGamePhaseTag(player.playerUsername, "steam_death", "Died", 50);
        }

        var localPlayer = StartOfRound.Instance.localPlayerController;

        /*
         * Least to most expensive checks, hopefully.
         *
         * 1. Check if the local player died
         * 2. Check if the local player observed the death in spectator mode
         * 3. Check if the local player is alive and the deceased is in close proximity
         * 4. Check if the local player is alive and has recently had line of sight on the deceased
         *
         * This should probably be adequate to prevent capture of deaths taking place on the other side of the map.
         */

        var shouldClip = false;

        if (localPlayer == player)
        {
            SteamHighlightsPlugin.Logger.LogDebug("It was us who died!");
            shouldClip = true;
        }
        else switch (localPlayer.isPlayerDead)
        {
            case true when localPlayer.spectatedPlayerScript == player:
                SteamHighlightsPlugin.Logger.LogDebug($"Observed {player.playerUsername} die in spectator mode");
                shouldClip = true;
                break;
            case false when IsPlayerNearby(player, 10f):
                SteamHighlightsPlugin.Logger.LogDebug($"{player.playerUsername} died near us");
                shouldClip = true;
                break;
            case false when VisibilityTracker.Instance.HasSeenRecently(player, VisibilityTracker.SecondsToCheck):
                SteamHighlightsPlugin.Logger.LogDebug($"{player.playerUsername} was seen within the last 20 seconds");
                shouldClip = true;
                break;
            default:
                SteamHighlightsPlugin.Logger.LogDebug("All conditions for marking the death as a clip failed, skipping clipping.");
                break;
        }

        if (shouldClip == false)
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
        if (SteamHighlightsPlugin.RecordingKindConfigEntry.Value == RecordingKind.Marker)
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
                -SteamHighlightsPlugin.PreDeathDurationConfigEntry.Value,
                SteamHighlightsPlugin.PreDeathDurationConfigEntry.Value + SteamHighlightsPlugin.PostDeathDurationConfigEntry.Value,
                TimelineEventClipPriority.Featured
            );
        }

        if (!SteamHighlightsPlugin.OpenOverlayConfigEntry.Value) yield break;
        
        if (SteamHighlightsPlugin.OnlyMyDeathsConfigEntry.Value && player != StartOfRound.Instance.localPlayerController)
        {
            yield break;
        }

        yield return new WaitForSecondsRealtime(SteamHighlightsPlugin.OverlayDelayConfigEntry.Value);
        SteamTimeline.OpenOverlayToTimelineEvent(handle);
    }
}