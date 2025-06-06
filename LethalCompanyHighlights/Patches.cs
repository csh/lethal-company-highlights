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