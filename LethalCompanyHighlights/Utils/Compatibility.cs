using System;
using System.Diagnostics.CodeAnalysis;
using BepInEx.Bootstrap;

namespace LethalCompanyHighlights.Utils;

// TODO: Find a nicer solution for this.
internal static class LobbyCompatibility
{
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute", Justification = "Exception handler will catch the NRE")]
    internal static void Check()
    {
        if (Chainloader.PluginInfos.TryGetValue("BMX.LobbyCompatibility", out var pluginInfo))
        {
            SteamHighlightsPlugin.Logger.LogInfo("LobbyCompatibility detected, attempting to register compatibility info.");
            try
            {
                var assembly = pluginInfo.Instance.GetType().Assembly;
                var compatibilityLevelEnum = assembly.GetType("LobbyCompatibility.Enums.CompatibilityLevel");
                var versionStrictnessEnum = assembly.GetType("LobbyCompatibility.Enums.VersionStrictness");
                var pluginHelperType = assembly.GetType("LobbyCompatibility.Features.PluginHelper");
                var registerMethod = pluginHelperType?.GetMethod(
                    "RegisterPlugin",
                    [typeof(string), typeof(Version), compatibilityLevelEnum, versionStrictnessEnum]
                );

                if (pluginHelperType != null && registerMethod != null)
                {
                    var clientOnly = Enum.Parse(compatibilityLevelEnum, "ClientOnly");
                    var none = Enum.Parse(versionStrictnessEnum, "None");

                    registerMethod.Invoke(null, [
                        PluginInfo.PLUGIN_GUID,
                        Version.Parse(PluginInfo.PLUGIN_VERSION),
                        clientOnly,
                        none
                    ]);
                    
                    SteamHighlightsPlugin.Logger.LogInfo("Registered with LobbyCompatibility");
                }
                else
                {
                    SteamHighlightsPlugin.Logger.LogWarning("LobbyCompatibility.RegisterPlugin method not found.");
                }
            }
            catch (Exception ex)
            {
                SteamHighlightsPlugin.Logger.LogError($"Failed to register with LobbyCompatibility: {ex}");
            }
        }
        else
        {
            SteamHighlightsPlugin.Logger.LogInfo("LobbyCompatibility not detected, skipping compatibility registration");
        }
    }
}