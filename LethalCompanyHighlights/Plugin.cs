using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using Steamworks;
using UnityEngine;

namespace LethalCompanyHighlights;

internal enum RecordingKind
{
    Marker,
    Clip
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("ainavt.lc.lethalconfig")]
[BepInDependency("com.elitemastereric.coroner")]
[BepInDependency("BMX.LobbyCompatibility", BepInDependency.DependencyFlags.SoftDependency)]
public class SteamHighlightsPlugin : BaseUnityPlugin
{
    internal static SteamHighlightsPlugin Instance { get; private set; }
    internal new static ManualLogSource Logger;

    internal static ConfigEntry<bool> IsEnabledConfigEntry;
    internal static ConfigEntry<bool> OpenOverlayConfigEntry;
    internal static ConfigEntry<bool> OnlyMyDeathsConfigEntry;
    internal static ConfigEntry<int> OverlayDelayConfigEntry;
    internal static ConfigEntry<int> PreDeathDurationConfigEntry;
    internal static ConfigEntry<int> PostDeathDurationConfigEntry;
    internal static ConfigEntry<RecordingKind> RecordingKindConfigEntry;

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
        
        IsEnabledConfigEntry = Config.Bind(
            "General",
            "Enable",
            true,
            "Enable Steam to capture highlights."
        );

        OpenOverlayConfigEntry = Config.Bind(
            "General",
            "Open Overlay on Death",
            true,
            "Would you like to open the Steam Overlay upon death?"
        );

        OnlyMyDeathsConfigEntry = Config.Bind(
            "General",
            "My Deaths Only",
            true,
            "Would you like to open the overlay for all deaths, or just yours?"
        );

        OverlayDelayConfigEntry = Config.Bind(
            "General",
            "Overlay Delay",
            5,
            "Delay in seconds before the Steam Overlay will open if enabled.\nMust be between 2 and 60 seconds."
        );

        PreDeathDurationConfigEntry = Config.Bind(
            "Recording",
            "Context Duration",
            10,
            "Duration to record prior to death.\nMust be between 5 and 60 seconds."
        );

        PostDeathDurationConfigEntry = Config.Bind(
            "Recording",
            "Post-Death Duration",
            10,
            "Duration to record after death.\nMust be between 5 and 60 seconds."
        );

        RecordingKindConfigEntry = Config.Bind(
            "General",
            "Recording Style",
            RecordingKind.Clip,
            "Choose the kind of recording to use.\n" +
            "Marker: Records a marker for the death event, has pretty icons you can click to jump to.\n" +
            "Duration not supported. \n" +
            "Clip: Marks a clip of the death event with duration support, easier for one-click sharing. No pretty icons :("
        );

        var enabledConfigToggle = new BoolCheckBoxConfigItem(IsEnabledConfigEntry, new BoolCheckBoxOptions
        {
            Name = "Enable Death Capture",
            Description = "Enable Steam to capture highlights, you must have Steam running and have Game Recording enabled in the Steam settings.",
            Section = "General",
            RequiresRestart = false,
            CanModifyCallback = CanModifySettings
        });

        var openOverlayConfigToggle = new BoolCheckBoxConfigItem(OpenOverlayConfigEntry, new BoolCheckBoxOptions
        {
            Name = "Open Overlay on Death",
            Description = "Would you like to open the Steam Overlay upon death?",
            Section = "Overlay Settings",
            RequiresRestart = false,
            CanModifyCallback = CanModifySettings
        });

        var onlyMyDeathsConfigToggle = new BoolCheckBoxConfigItem(OnlyMyDeathsConfigEntry, new BoolCheckBoxOptions
        {
            Name = "My Deaths Only",
            Description = "Would you like to open the overlay for all deaths, or just yours?",
            Section = "Overlay Settings",
            RequiresRestart = false,
            CanModifyCallback = CanModifyOverlaySettings
        });

        var overlayDelaySlider = new IntSliderConfigItem(OverlayDelayConfigEntry, new IntSliderOptions
        {
            Name = "Overlay Delay",
            Description = "Delay in seconds before the Steam Overlay will open if enabled.\nMust be between 2 and 60 seconds.",
            Section = "Overlay Settings",
            Min = 2,
            Max = 60,
            RequiresRestart = false,
            CanModifyCallback = CanModifyOverlaySettings
        });

        var recordingKindDropdown = new EnumDropDownConfigItem<RecordingKind>(RecordingKindConfigEntry, new EnumDropDownOptions
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

        var preDeathDurationSlider = new IntSliderConfigItem(PreDeathDurationConfigEntry, new IntSliderOptions
        {
            Name = "Context Duration",
            Description = "Duration to record prior to death.\nMust be between 5 and 60 seconds.",
            Section = "Recording",
            Min = 5,
            Max = 60,
            RequiresRestart = false,
            CanModifyCallback = CanModifyDurationSettings
        });

        var postDeathDurationSlider = new IntSliderConfigItem(PostDeathDurationConfigEntry, new IntSliderOptions
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
        if ((GameNetworkManager.Instance && GameNetworkManager.Instance.disableSteam == false) && IsEnabledConfigEntry.Value)
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

    private static CanModifyResult CanModifyOverlaySettings()
    {
        if (SteamUtils.IsOverlayEnabled && OpenOverlayConfigEntry.Value)
        {
            return CanModifyResult.True();
        }
        else
        {
            return CanModifyResult.False("Steam Overlay is disabled or you have not enabled the option to open the overlay on death.");
        }
    }

    private static CanModifyResult CanModifyDurationSettings()
    {
        return RecordingKindConfigEntry.Value switch
        {
            RecordingKind.Marker => CanModifyResult.False("Duration settings are not applicable for Marker recordings."),
            RecordingKind.Clip => CanModifyResult.True(),
            _ => CanModifyResult.False("Unknown recording kind selected.")
        };
    }

    private static CanModifyResult CanModifySettings()
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