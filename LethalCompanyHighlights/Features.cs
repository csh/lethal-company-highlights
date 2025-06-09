using LethalCompanyHighlights.Configuration;

namespace LethalCompanyHighlights;

internal static class Features
{
    internal static bool IsEnabled()
    {
        return PluginConfig.IsEnabledConfigEntry.Value;
    }
    
    internal static bool IsClippingEnabledForOthers()
    {
        return PluginConfig.OnlyClipMyDeathsConfigEntry.Value == false;
    }
    
    internal static bool IsProximityEnabled()
    {
        return IsClippingEnabledForOthers() && PluginConfig.ProximityCheckConfigEntry.Value;
    }

    internal static bool IsVisibilityEnabled()
    {
        return IsClippingEnabledForOthers() && PluginConfig.VisibilityCheckConfigEntry.Value;
    }
}