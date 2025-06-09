using BepInEx.Configuration;

namespace LethalCompanyHighlights.Configuration;

internal enum RecordingKind
{
    Marker,
    Clip
}

internal static class PluginConfig
{
    internal static ConfigEntry<bool> IsEnabledConfigEntry;
    internal static ConfigEntry<bool> OpenOverlayConfigEntry;
    internal static ConfigEntry<bool> OnlyMyDeathsConfigEntry;
    internal static ConfigEntry<int> OverlayDelayConfigEntry;
    internal static ConfigEntry<int> PreDeathDurationConfigEntry;
    internal static ConfigEntry<int> PostDeathDurationConfigEntry;
    internal static ConfigEntry<RecordingKind> RecordingKindConfigEntry;
    internal static ConfigEntry<bool> OnlyClipMyDeathsConfigEntry;
    internal static ConfigEntry<bool> ProximityCheckConfigEntry;
    internal static ConfigEntry<bool> VisibilityCheckConfigEntry;

    internal static void Init(ConfigFile config)
    {
        IsEnabledConfigEntry = config.Bind(
            "General",
            "Enable",
            true,
            "Enable Steam to capture highlights."
        );

        OpenOverlayConfigEntry = config.Bind(
            "General",
            "Open Overlay on Death",
            true,
            "Would you like to open the Steam Overlay upon death?"
        );

        OnlyMyDeathsConfigEntry = config.Bind(
            "General",
            "My Deaths Only",
            true,
            "Would you like to open the overlay for all deaths, or just yours?"
        );

        OverlayDelayConfigEntry = config.Bind(
            "General",
            "Overlay Delay",
            5,
            "Delay in seconds before the Steam Overlay will open if enabled.\nMust be between 2 and 60 seconds."
        );

        PreDeathDurationConfigEntry = config.Bind(
            "Recording",
            "Context Duration",
            10,
            "Duration to record prior to death.\nMust be between 5 and 60 seconds."
        );

        PostDeathDurationConfigEntry = config.Bind(
            "Recording",
            "Post-Death Duration",
            10,
            "Duration to record after death.\nMust be between 5 and 60 seconds."
        );

        RecordingKindConfigEntry = config.Bind(
            "General",
            "Recording Style",
            RecordingKind.Clip,
            "Choose the kind of recording to use.\n" +
            "Marker: Records a marker for the death event, has pretty icons you can click to jump to.\n" +
            "Duration not supported. \n" +
            "Clip: Marks a clip of the death event with duration support, easier for one-click sharing. No pretty icons :("
        );

        OnlyClipMyDeathsConfigEntry = config.Bind(
            "Death Capture",
            "Only Clip My Deaths",
            false,
            "Would you like to capture the deaths of other players, or just your own?"
        );

        ProximityCheckConfigEntry = config.Bind(
            "Death Capture",
            "Proximity Check",
            true,
            "Would you like to capture a clip when somebody dies near you?"
        );

        VisibilityCheckConfigEntry = config.Bind(
            "Death Capture",
            "Visibility Check",
            true,
            "Would you like to capture a clip when somebody you've seen within the last 10 seconds dies?"
        );
    }
}