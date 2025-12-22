using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Text;
using Dalamud.Utility;
using GatherBuddy.Automation;
using GatherBuddy.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace GatherBuddy.AutoGather
{
    public partial class AutoGather
    {
        private static readonly List<string> CollectablePatterns =
        [
            "collectability of",
            "収集価値",
            "Sammlerwert",
            "Valeur de collection",
            "收藏价值"
        ];

        private unsafe bool HandleFishingCollectable()
        {
            if (!GatherBuddy.Config.AutoGatherConfig.AutoCollectablesFishing)
            {
                GatherBuddy.Log.Debug("[AutoCollectable] Feature disabled in config");
                return false;
            }

            var addon = SelectYesnoAddon;
            if (addon == null)
                return false;
            
            if (!addon->IsReady)
            {
                GatherBuddy.Log.Debug("[AutoCollectable] SelectYesno addon not ready");
                return false;
            }
            
            GatherBuddy.Log.Debug("[AutoCollectable] SelectYesno addon found and ready");

            var master = new AddonMaster.SelectYesno(addon);
            var text = master.TextLegacy;
            GatherBuddy.Log.Debug($"[AutoCollectable] Read text: '{text}' (length={text.Length})");

            if (!CollectablePatterns.Any(text.Contains))
            {
                GatherBuddy.Log.Debug($"[AutoCollectable] Text does not match any collectable patterns");
                return false;
            }

            GatherBuddy.Log.Debug($"[AutoCollectable] Detected collectable dialog with text: {text}");

            var name = Enum.GetValues<SeIconChar>()
                .Cast<SeIconChar>()
                .Aggregate(addon->AtkValues[15].String.AsDalamudSeString().TextValue, 
                    (current, enumValue) => current.Replace(enumValue.ToIconString(), ""))
                .Trim();

            var itemSheet = Dalamud.GameData.GetExcelSheet<Item>();
            var item = itemSheet.FirstOrDefault(x => x.IsCollectable && !x.Singular.IsEmpty && 
                name.Contains(x.Singular.ToString(), StringComparison.InvariantCultureIgnoreCase));
            if (item.RowId == 0)
            {
                GatherBuddy.Log.Debug($"[AutoCollectable] Failed to match any collectable to {name} [original={addon->AtkValues[15].String}]");
                return false;
            }

            GatherBuddy.Log.Debug($"[AutoCollectable] Detected item [{item.RowId}] {item.Name}");

            if (!int.TryParse(Regex.Match(text, @"\d+").Value, out var value))
            {
                GatherBuddy.Log.Debug($"[AutoCollectable] Failed to parse collectability value from text");
                return false;
            }

            GatherBuddy.Log.Debug($"[AutoCollectable] Detected collectability value: {value}");
            GatherBuddy.Log.Debug($"[AutoCollectable] Item data - AetherialReduce: {item.AetherialReduce}, AdditionalData.RowId: {item.AdditionalData.RowId}");
            {
                if (item.AetherialReduce > 0)
                {
                    GatherBuddy.Log.Debug($"[AutoCollectable] Accepting [{item.RowId}] {item.Name} - aethersand fish");
                    Callback.Fire(&addon->AtkUnitBase, true, 0);
                    return true;
                }
                else if (item.AdditionalData.RowId != 0)
                {
                    var wksItem = Dalamud.GameData.GetExcelSheet<WKSItemInfo>().GetRow(item.AdditionalData.RowId);
                    if (wksItem.RowId != 0)
                {
                        GatherBuddy.Log.Debug($"[AutoCollectable] Accepting [{item.RowId}] {item.Name} - stellar fish for {wksItem.WKSItemSubCategory.ValueNullable?.Name ?? "null"}");
                        Callback.Fire(&addon->AtkUnitBase, true, 0);
                        return true;
                    }
                    else
                    {
                        GatherBuddy.Log.Debug($"[AutoCollectable] No CollectablesShopItem found for [{item.RowId}] {item.Name}");
                    }
                }
                else
                {
                    GatherBuddy.Log.Debug($"[AutoCollectable] No CollectablesShopItem found for [{item.RowId}] {item.Name}");
                }
            }

            GatherBuddy.Log.Debug($"[AutoCollectable] Accepting [{item.RowId}] {item.Name} - generic collectable fish with value {value}");
            Callback.Fire(&addon->AtkUnitBase, true, 0);
            return true;
        }
    }
}
