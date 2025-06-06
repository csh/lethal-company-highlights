using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace LethalCompanyHighlights;

public class VisibilityTracker : MonoBehaviour
{
    internal bool isRunning = true;

    private IDictionary<ulong, float> lastSeen;

    private Coroutine visibilityCoroutine;

    public void Initialize(IDictionary<ulong, float> lastSeen)
    {
        this.lastSeen = lastSeen;
        isRunning = true;
        visibilityCoroutine = StartCoroutine(UpdateVisibility());
    }

    static bool IsPlayerVisible(PlayerControllerB from, PlayerControllerB to, float maxDistance)
    {
        Vector3 eyeOrigin = from.gameplayCamera.transform.position;
        Vector3 targetPos = to.transform.position + Vector3.up * 0.5f;

        if (Vector3.Distance(eyeOrigin, targetPos) > maxDistance)
            return false;

        return !Physics.Linecast(eyeOrigin, targetPos, StartOfRound.Instance.collidersAndRoomMaskAndPlayers);
    }

    private IEnumerator UpdateVisibility()
    {
        while (isRunning)
        {
            var localPlayer = StartOfRound.Instance.localPlayerController;

            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player == localPlayer || player.isActiveAndEnabled == false || player.isPlayerDead) continue;
                if (!IsPlayerVisible(localPlayer, player, 30f)) continue;
                lastSeen[player.playerClientId] = Time.time;
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    public void OnDestroy()
    {
        isRunning = false;

        if (visibilityCoroutine != null)
        {
            StopCoroutine(visibilityCoroutine);
        }
    }
}