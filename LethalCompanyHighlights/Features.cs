using System;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using GameNetcodeStuff;
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

    private static readonly bool IsCoronerLoaded = Chainloader.PluginInfos.ContainsKey("com.elitemastereric.coroner");
    
    internal static string GetCauseOfDeath(PlayerControllerB player)
    {
        return IsCoronerLoaded ? GetCoronerCauseOfDeath(player) : GetVanillaCauseOfDeath(player);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string GetCoronerCauseOfDeath(PlayerControllerB player)
    {
        var cause = Coroner.API.GetCauseOfDeath(player);
        return cause.HasValue
            ? Coroner.API.StringifyCauseOfDeath(cause.Value, null)
            : GetVanillaCauseOfDeath(player);
    }
    
    private static string GetVanillaCauseOfDeath(PlayerControllerB player)
    {
        switch (player.causeOfDeath)
        {
            case CauseOfDeath.Bludgeoning:
                return "bludgeoned to death";
            case CauseOfDeath.Gravity:
                return "fell";
            case CauseOfDeath.Blast:
                return "blew up";
            case CauseOfDeath.Strangulation:
            case CauseOfDeath.Suffocation:
                return "died of asphyxiation";
            case CauseOfDeath.Mauling:
                return "mauled to death";
            case CauseOfDeath.Gunshots:
                return "caught in a firefight";
            case CauseOfDeath.Crushing:
                return "crushed";
            case CauseOfDeath.Drowning:
                return "drowned";
            case CauseOfDeath.Abandoned:
                return "left behind";
            case CauseOfDeath.Electrocution:
                return "electrocuted";
            case CauseOfDeath.Kicking:
                return "kicked to death";
            case CauseOfDeath.Burning:
                return "burned alive";
            case CauseOfDeath.Stabbing:
                return "stabbed";
            case CauseOfDeath.Snipping:
                return "chopped in two";
            case CauseOfDeath.Unknown:
            case CauseOfDeath.Fan:
            case CauseOfDeath.Inertia:
            case CauseOfDeath.Scratching:
            default:
                return "died of unknown causes";
        }
    }
}