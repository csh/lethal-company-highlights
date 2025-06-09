using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using GameNetcodeStuff;
using UnityEngine;

namespace LethalCompanyHighlights.Utils;

public class VisibilityTracker : MonoBehaviour
{
    internal static VisibilityTracker Instance { get; private set; }
    
    private const float DefaultFieldOfView = 66f;
    private const float DistanceToCheck = 20f;
    internal const float SecondsToCheck = 20f;

    private readonly IDictionary<ulong, float> _lastSeen = new Dictionary<ulong, float>();
    private readonly IList<ulong> _stalePlayerIds = new List<ulong>();
    private Coroutine _visibilityCoroutine;
    private bool _isRunning = true;

    public void Awake()
    {
        if (Instance)
        {
            SteamHighlightsPlugin.Logger.LogWarning($"A new instance of {nameof(VisibilityTracker)} has been created, destroying the old one.");
            Destroy(Instance);
        }
        Instance = this;
    }
    
    public void Initialize()
    {
        _isRunning = true;
        _visibilityCoroutine = StartCoroutine(UpdateVisibility());
    }

    private static bool IsPlayerVisible(PlayerControllerB from, PlayerControllerB to, float maxDistance)
    {
        var origin = from.gameplayCamera
            ? from.gameplayCamera.transform.position
            : from.transform.position + Vector3.up * 0.6f;

        var forward = from.gameplayCamera
            ? from.gameplayCamera.transform.forward
            : from.transform.forward;

        var target = to.transform.position + Vector3.up * 0.6f;
        var toTarget = (target - origin);
        var sqrDistance = toTarget.sqrMagnitude;

        if (sqrDistance > maxDistance * maxDistance)
        {
            return false;
        }

        var dirToTarget = toTarget.normalized;
        var dot = Vector3.Dot(forward, dirToTarget);
        var fov = Mathf.Clamp(from.gameplayCamera ? from.gameplayCamera.fieldOfView : DefaultFieldOfView, DefaultFieldOfView, 150f);
        var cosHalfFOV = Mathf.Cos(fov * 0.5f * Mathf.Deg2Rad);

        if (dot < cosHalfFOV)
        {
            return false;
        }

        if (Physics.Linecast(origin, target, out _, StartOfRound.Instance.collidersAndRoomMask))
        {
            SteamHighlightsPlugin.Logger.LogDebug($"Player '{to.playerUsername}' is occluded by something");
            return false;
        }

        SteamHighlightsPlugin.Logger.LogDebug($"Player '{to.playerUsername}' is visible");
        return true;
    }

    [SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery")]
    private IEnumerator UpdateVisibility()
    {
        var localPlayer = GetComponentInParent<PlayerControllerB>();
        while (Features.IsVisibilityEnabled() && _isRunning && IsHumanPlayerAlive(localPlayer))
        {
            var now = Time.unscaledTime;
            foreach (var (playerId, seenAt) in _lastSeen)
            {
                if (now - seenAt > SecondsToCheck)
                {
                    _stalePlayerIds.Add(playerId);
                }
            }
            
            foreach (var playerId in _stalePlayerIds)
            {
                _lastSeen.Remove(playerId);
            }
            
            _stalePlayerIds.Clear();
            
            foreach (var player in StartOfRound.Instance.allPlayerScripts.Where(player => IsHumanPlayerAlive(player) && player != localPlayer && IsPlayerVisible(localPlayer, player, DistanceToCheck)))
            {
                _lastSeen[player.playerClientId] = now;
            }

            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    private static bool IsHumanPlayerAlive(PlayerControllerB player)
    {
        return player && player.isPlayerControlled && player.isPlayerDead == false;
    }

    public void StopVisibilityCoroutine()
    {
        _isRunning = false;
        _lastSeen.Clear();
        _stalePlayerIds.Clear();
        if (_visibilityCoroutine != null)
        {
            StopCoroutine(_visibilityCoroutine);
        }
    }

    public bool HasSeenRecently(PlayerControllerB other, float window = SecondsToCheck)
    {
        return _lastSeen.TryGetValue(other.playerClientId, out var seenTime) && (Time.unscaledTime - seenTime) <= window;
    }

    public void OnDestroy()
    {
        StopVisibilityCoroutine();
    }
}