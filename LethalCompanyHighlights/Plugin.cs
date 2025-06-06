using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using Steamworks;
using Unity.Netcode;

namespace LethalCompanyHighlights;

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

        StartOfRound.Instance.NetworkManager.OnClientDisconnectCallback += PlayerPatches.RemoveFromLastSeen;
    }

    private void OnDestroy()
    {
        StartOfRound.Instance.NetworkManager.OnClientDisconnectCallback -= PlayerPatches.RemoveFromLastSeen;

        if (isEnabledConfigEntry.Value)
        {
            SteamTimeline.EndGamePhase();
        }
        harmony.UnpatchSelf();

        if (StartOfRound.Instance.localPlayerController.gameObject.TryGetComponent<VisibilityTracker>(out var tracker))
        {
            Destroy(tracker);
        }
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