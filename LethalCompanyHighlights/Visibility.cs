using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.UI;

namespace LethalCompanyHighlights;

public class VisibilityTracker : MonoBehaviour
{
    private bool isRunning = true;

    private readonly IDictionary<ulong, float> lastSeen = new Dictionary<ulong, float>();

    private Coroutine visibilityCoroutine;

    public void Initialize()
    {
        isRunning = true;
        visibilityCoroutine = StartCoroutine(UpdateVisibility());

#if DEBUG
        StartCoroutine(InitDebugHud());
#endif
    }

    // TODO: More efficiency?
    bool IsPlayerVisible(PlayerControllerB from, PlayerControllerB to, float maxDistance)
    {
        Vector3 origin = from.gameplayCamera != null
        ? from.gameplayCamera.transform.position
        : from.transform.position + Vector3.up * 1.5f;

        Vector3 target = to.transform.position + Vector3.up * 1.5f;

        if ((origin - target).sqrMagnitude > maxDistance * maxDistance)
            return false;

        if (Physics.Linecast(origin, target, out RaycastHit hit, StartOfRound.Instance.collidersAndRoomMask))
        {
            var hitPlayer = hit.collider.GetComponentInParent<PlayerControllerB>();
            return hitPlayer == to;
        }

        return true;
    }

    private IEnumerator UpdateVisibility()
    {
        var localPlayer = GetComponentInParent<PlayerControllerB>();
        while (isRunning && localPlayer.isActiveAndEnabled)
        {
            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player == localPlayer || player.isActiveAndEnabled == false || player.isPlayerDead) continue;
                if (!IsPlayerVisible(localPlayer, player, 30f)) continue;
                lastSeen[player.playerClientId] = Time.unscaledTime;
            }

#if DEBUG
            UpdateDebugOverlay();
#endif

            yield return new WaitForSeconds(0.5f);
        }
    }

#if DEBUG
    private bool isDebug = false;
    private GameObject _textObject;
    private Text _uiText;

    private void UpdateDebugOverlay()
    {
        if (isDebug && _uiText != null)
        {

            if (lastSeen.Count == 0)
            {
                _uiText.text = "Highlight Debug HUD:\n"
                             + "Nothing to report, you're alone.";
            }
            else
            {
                var builder = new StringBuilder();
                foreach (var playerId in lastSeen.Keys)
                {
                    var player = StartOfRound.Instance.allPlayerScripts[playerId];
                    if (player == null || player.isActiveAndEnabled == false) continue;
                    builder.Append($"{player.playerUsername} => {HasSeenRecently(player, 30)}\n");
                }
                _uiText.text = $"Highlight Debug HUD:\n{builder}";
            }
        }
    }

    public void Update()
    {
        if (Input.GetKey(KeyCode.F10))
        {
            isDebug = !isDebug;
        }
    }

    private IEnumerator InitDebugHud()
    {
        yield return new WaitForSeconds(2f);

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGameObject = new GameObject("CustomHUDCanvas");
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

        RectTransform rectTransform = _uiText.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1, 0); 
        rectTransform.anchorMax = new Vector2(1, 0);
        rectTransform.pivot = new Vector2(1, 0);      
        rectTransform.anchoredPosition = new Vector2(-20, 20); 
        rectTransform.sizeDelta = new Vector2(400, 60);
    }
#endif

    public void StopVisibilityCoroutine()
    {
        isRunning = false;
        Clear();
        if (visibilityCoroutine != null)
        {
            StopCoroutine(visibilityCoroutine);
        }
    }

    public void Clear()
    {
        lastSeen.Clear();
    }

    public bool HasSeenRecently(PlayerControllerB other, float window)
    {
        return lastSeen.TryGetValue(other.playerClientId, out var seenTime) && (Time.unscaledTime - seenTime) <= window;
    }

    public void OnDestroy()
    {
        StopVisibilityCoroutine();
    }
}