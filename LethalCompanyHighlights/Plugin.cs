using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalCompanyHighlights.Configuration;
using LethalCompanyHighlights.Utils;
using Steamworks;
using UnityEngine;

namespace LethalCompanyHighlights;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("ainavt.lc.lethalconfig")]
[BepInDependency("com.elitemastereric.coroner")]
[BepInDependency("BMX.LobbyCompatibility", BepInDependency.DependencyFlags.SoftDependency)]
public class SteamHighlightsPlugin : BaseUnityPlugin
{
    internal static SteamHighlightsPlugin Instance { get; private set; }
    internal new static ManualLogSource Logger;

    private Harmony _harmony;

#pragma warning disable IDE0051
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        gameObject.transform.parent = null;
        gameObject.hideFlags = HideFlags.HideAndDontSave;

        Instance = this;
        Logger = base.Logger;

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_NAME} is loaded!");

        LobbyCompatibility.Check();

        PluginConfig.Init(Config);
        PluginConfigUI.Init();

        if (GameNetworkManager.Instance && GameNetworkManager.Instance.disableSteam)
        {
            Logger.LogWarning("Steam integration is disabled, if you're playing in LAN mode I'm afraid this mod won't work.");
            return;
        }

        _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        _harmony.PatchAll(typeof(RoundPatches));
        _harmony.PatchAll(typeof(PlayerPatches));
    }

    private void OnDestroy()
    {
        if ((GameNetworkManager.Instance && GameNetworkManager.Instance.disableSteam == false) && Features.IsEnabled())
        {
            SteamTimeline.EndGamePhase();
        }
        _harmony?.UnpatchSelf();

        // Ensure we cleanup any currently running coroutines
        foreach (var tracker in FindObjectsOfType<VisibilityTracker>())
        {
            Destroy(tracker);
        }
    }
}