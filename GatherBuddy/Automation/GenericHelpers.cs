using System;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GatherBuddy.Plugin;

namespace GatherBuddy.Automation;

public static unsafe class GenericHelpers
{
    public static bool TryGetAddonByName<T>(string addonName, out T* addon) where T : unmanaged
    {
        var ptr = Dalamud.GameGui.GetAddonByName(addonName);
        addon = (T*)(nint)ptr;
        if (addon == null)
            return false;

        var atkUnitBase = (AtkUnitBase*)addon;
        return atkUnitBase->IsFullyLoaded() && atkUnitBase->IsReady;
    }

    public static bool TryGetAddonByName(string addonName, out AtkUnitBase* addon)
    {
        return TryGetAddonByName<AtkUnitBase>(addonName, out addon);
    }

    public static bool IsAddonReady(AtkUnitBase* addon)
    {
        if (addon == null)
            return false;
        return addon->IsFullyLoaded() && addon->IsReady;
    }

    public static bool IsScreenReady()
    {
        var ptr = Dalamud.GameGui.GetAddonByName("_ScreenInfo");
        return (nint)ptr != IntPtr.Zero;
    }
}
