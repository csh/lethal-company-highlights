using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.UI;

namespace LethalCompanyHighlights;

public class VisibilityTracker : MonoBehaviour
{
    internal static VisibilityTracker Instance { get; private set; }

    private readonly IDictionary<ulong, float> _lastSeen = new Dictionary<ulong, float>();
    private Coroutine _visibilityCoroutine;
    private bool _isRunning = true;

    public void Awake()
    {
        if (Instance)
        {
            Destroy(Instance);
        }
        Instance = this;
    }
    
    public void Initialize()
    {
        _isRunning = true;
        _visibilityCoroutine = StartCoroutine(UpdateVisibility());
        
#if DEBUG
        StartCoroutine(InitDebugHud());
#endif
    }

    // TODO: More efficiency?
    private static bool IsPlayerVisible(PlayerControllerB from, PlayerControllerB to, float maxDistance)
    {
        var origin = from.gameplayCamera
        ? from.gameplayCamera.transform.position
        : from.transform.position + Vector3.up * 0.6f;

        var target = to.transform.position + Vector3.up * 0.6f;
        if ((origin - target).sqrMagnitude > maxDistance * maxDistance)
        {
            SteamHighlightsPlugin.Logger.LogDebug($"Player '{to.playerUsername}' is outside of maxDistance");
            return false;
        }

        if (!Physics.Linecast(origin, target, out var hit, StartOfRound.Instance.collidersAndRoomMask))
        {
            SteamHighlightsPlugin.Logger.LogDebug($"Player '{to.playerUsername}' is not occluded by anything");
            return true;
        }
        
        var hitPlayer = hit.collider.GetComponentInParent<PlayerControllerB>();
        return hitPlayer == to;
    }

    private IEnumerator UpdateVisibility()
    {
        var localPlayer = GetComponentInParent<PlayerControllerB>();
        while (_isRunning && localPlayer.isActiveAndEnabled)
        {
            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player == localPlayer || player.isActiveAndEnabled == false || player.isPlayerDead) continue;
                if (!IsPlayerVisible(localPlayer, player, 30f)) continue;
                _lastSeen[player.playerClientId] = Time.unscaledTime;
            }

#if DEBUG
            UpdateDebugOverlay();
#endif

            yield return new WaitForSeconds(0.5f);
        }
    }

#if DEBUG
    private bool _isDebug = true;
    private GameObject _textObject;
    private Text _uiText;

    private void UpdateDebugOverlay()
    {
        if (!_isDebug || !_uiText) return;
        
        if (_lastSeen.Count == 0)
        {
            _uiText.text = "Highlight Debug HUD:\n"
                           + "Nothing to report, you're alone.";
        }
        else
        {
            var builder = new StringBuilder();
            foreach (var playerId in _lastSeen.Keys)
            {
                var player = StartOfRound.Instance.allPlayerScripts[playerId];
                if (!player || player.isActiveAndEnabled == false) continue;
                builder.Append($"{player.playerUsername} => {HasSeenRecently(player, 20)}\n");
            }
            _uiText.text = $"Highlight Debug HUD:\n{builder}";
        }
    }

    public void Update()
    {
        if (Input.GetKey(KeyCode.F10))
        {
            _isDebug = !_isDebug;
        }
    }

    private IEnumerator InitDebugHud()
    {
        if (_uiText) yield break;
        
        yield return new WaitForSeconds(2f);

        var canvas = FindObjectOfType<Canvas>();
        if (!canvas)
        {
            var canvasGameObject = new GameObject("CustomHUDCanvas");
            canvas = canvasGameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGameObject.AddComponent<CanvasScaler>();
            canvasGameObject.AddComponent<GraphicRaycaster>();
        }

        _textObject = new GameObject("HighlightVisibilityDebugHUD");
        _textObject.transform.SetParent(canvas.transform);

        _uiText = _textObject.AddComponent<Text>();
        _uiText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _uiText.text = "Highlight Debug HUD Initialized!";
        _uiText.fontSize = 24;
        _uiText.color = Color.white;
        _uiText.alignment = TextAnchor.LowerRight;

        var rectTransform = _uiText.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1, 0); 
        rectTransform.anchorMax = new Vector2(1, 0);
        rectTransform.pivot = new Vector2(1, 0);      
        rectTransform.anchoredPosition = new Vector2(-20, 20); 
        rectTransform.sizeDelta = new Vector2(400, 60);
    }
#endif

    public void StopVisibilityCoroutine()
    {
        _isRunning = false;
        _lastSeen.Clear();
        if (_visibilityCoroutine != null)
        {
            StopCoroutine(_visibilityCoroutine);
        }
    }

    public bool HasSeenRecently(PlayerControllerB other, float window)
    {
        return _lastSeen.TryGetValue(other.playerClientId, out var seenTime) && (Time.unscaledTime - seenTime) <= window;
    }

    public void OnDestroy()
    {
        StopVisibilityCoroutine();
    }
}