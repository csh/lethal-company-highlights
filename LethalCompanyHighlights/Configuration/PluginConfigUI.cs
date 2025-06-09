using LethalCompanyHighlights.Utils;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using Steamworks;

namespace LethalCompanyHighlights.Configuration;

internal static class PluginConfigUI
{
    internal static void Init()
    {
        var enabledConfigToggle = new BoolCheckBoxConfigItem(PluginConfig.IsEnabledConfigEntry, new BoolCheckBoxOptions
        {
            Name = "Enable Death Capture",
            Description = "Enable Steam to capture highlights, you must have Steam running and have Game Recording enabled in the Steam settings.",
            Section = "General",
            RequiresRestart = false,
            CanModifyCallback = CanModifySettings
        });

        var openOverlayConfigToggle = new BoolCheckBoxConfigItem(PluginConfig.OpenOverlayConfigEntry, new BoolCheckBoxOptions
        {
            Name = "Open Overlay on Death",
            Description = "Would you like to open the Steam Overlay upon death?",
            Section = "Overlay Settings",
            RequiresRestart = false,
            CanModifyCallback = CanModifySettings
        });

        var onlyMyDeathsConfigToggle = new BoolCheckBoxConfigItem(PluginConfig.OnlyMyDeathsConfigEntry, new BoolCheckBoxOptions
        {
            Name = "My Deaths Only",
            Description = "Would you like to open the overlay for all deaths, or just yours?",
            Section = "Overlay Settings",
            RequiresRestart = false,
            CanModifyCallback = CanModifyOverlaySettings
        });

        var overlayDelaySlider = new IntSliderConfigItem(PluginConfig.OverlayDelayConfigEntry, new IntSliderOptions
        {
            Name = "Overlay Delay",
            Description = "Delay in seconds before the Steam Overlay will open if enabled.\nMust be between 2 and 60 seconds.",
            Section = "Overlay Settings",
            Min = 2,
            Max = 60,
            RequiresRestart = false,
            CanModifyCallback = CanModifyOverlaySettings
        });

        var onlyClipMyDeathsConfigToggle = new BoolCheckBoxConfigItem(PluginConfig.OnlyClipMyDeathsConfigEntry, new BoolCheckBoxOptions
        {
            Name = "Only Clip My Deaths",
            Description = "Would you like to capture the deaths of other players, or just your own?",
            Section = "Death Capture",
            RequiresRestart = false,
            CanModifyCallback = CanModifySettings
        });

        var proximityCheckConfigToggle = new BoolCheckBoxConfigItem(PluginConfig.ProximityCheckConfigEntry, new BoolCheckBoxOptions
        {
            Name = "Proximity Check",
            Description = "Would you like to capture a clip when somebody dies near you?",
            Section = "Death Capture",
            RequiresRestart = false,
            CanModifyCallback = CanModifyDeathCheckSettings
        });

        var visibilityCheckConfigToggle = new BoolCheckBoxConfigItem(PluginConfig.VisibilityCheckConfigEntry, new BoolCheckBoxOptions
        {
            Name = "Visibility Check",
            Description = $"Would you like to capture a clip when somebody you've seen within the last {VisibilityTracker.SecondsToCheck} seconds dies?",
            Section = "Death Capture",
            RequiresRestart = false,
            CanModifyCallback = CanModifyDeathCheckSettings
        });

        var recordingKindDropdown = new EnumDropDownConfigItem<RecordingKind>(PluginConfig.RecordingKindConfigEntry, new EnumDropDownOptions
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

        var preDeathDurationSlider = new IntSliderConfigItem(PluginConfig.PreDeathDurationConfigEntry, new IntSliderOptions
        {
            Name = "Context Duration",
            Description = "Duration to record prior to death.\nMust be between 5 and 60 seconds.",
            Section = "Recording",
            Min = 5,
            Max = 60,
            RequiresRestart = false,
            CanModifyCallback = CanModifyDurationSettings
        });

        var postDeathDurationSlider = new IntSliderConfigItem(PluginConfig.PostDeathDurationConfigEntry, new IntSliderOptions
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

        LethalConfigManager.AddConfigItem(onlyClipMyDeathsConfigToggle);
        LethalConfigManager.AddConfigItem(proximityCheckConfigToggle);
        LethalConfigManager.AddConfigItem(visibilityCheckConfigToggle);

        LethalConfigManager.AddConfigItem(recordingKindDropdown);
        LethalConfigManager.AddConfigItem(preDeathDurationSlider);
        LethalConfigManager.AddConfigItem(postDeathDurationSlider);
    }
    
    private static CanModifyResult CanModifyOverlaySettings()
    {
        if (SteamUtils.IsOverlayEnabled && PluginConfig.OpenOverlayConfigEntry.Value)
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
        return PluginConfig.RecordingKindConfigEntry.Value switch
        {
            RecordingKind.Marker => CanModifyResult.False("Duration settings are not applicable for Marker recordings."),
            RecordingKind.Clip => CanModifyResult.True(),
            _ => CanModifyResult.False("Unknown recording kind selected.")
        };
    }

    private static CanModifyResult CanModifyDeathCheckSettings()
    {
        if (PluginConfig.OnlyClipMyDeathsConfigEntry.Value)
        {
            return CanModifyResult.False($"You must disable 'Only Clip My Deaths' to use this feature.");
        }
        else
        {
            return CanModifyResult.True();
        }
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