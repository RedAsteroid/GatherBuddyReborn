using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace GatherBuddy.AutoGather.Collectables;

public static class ItemJobResolver
{
    public static int GetJobIdForItem(string itemName, IDataManager data)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return -1;

        itemName = itemName.Replace(" \uE03D", "").ToLowerInvariant();

        var item = data.GetExcelSheet<Item>()?
            .FirstOrDefault(i => i.Name.ToString().ToLowerInvariant() == itemName);
        if (item == null)
            return -1;

        uint itemId = item.Value.RowId;

        var recipeSheet = data.GetExcelSheet<Recipe>();
        if (recipeSheet != null)
        {
            var recipe = recipeSheet.FirstOrDefault(r => r.ItemResult.RowId == itemId);
            if (((Lumina.Excel.IExcelRow<Recipe>)recipe).RowId != 0)
                return (int)recipe.CraftType.RowId;
        }

        var fishSheet = data.GetExcelSheet<FishParameter>();
        if (fishSheet?.Any(f => f.Item.RowId == itemId) == true)
            return 10;
        
        var spearSheet = data.GetExcelSheet<SpearfishingItem>();
        if (spearSheet?.Any(f => f.Item.RowId == itemId) == true)
            return 10;
        
        var giSheet  = data.GetExcelSheet<GatheringItem>();
        var gpbSheet = data.GetExcelSheet<GatheringPointBase>();
        if (giSheet == null || gpbSheet == null)
            return -1;

        var gi = giSheet.FirstOrDefault(g => g.Item.RowId == itemId);
        var gatherId = gi.RowId;

        foreach (var b in gpbSheet)
        {
            for (int i = 0; i < b.Item.Count; i++)
            {
                if (b.Item[i].RowId == gatherId)
                    return MapTypeToJob(b);
            }
        }

        var gipSheet = data.GetSubrowExcelSheet<GatheringItemPoint>();
        if (gipSheet != null)
        {
            foreach (var gip in gipSheet.SelectMany(s => s))
            {
                if (gip.RowId != gatherId)
                    continue;

                var gp = gip.GatheringPoint.ValueNullable;
                if (gp == null) continue;

                var baseRow = gp.Value.GatheringPointBase.ValueNullable;
                if (baseRow == null) continue;

                return MapTypeToJob(baseRow.Value);
            }
        }

        return -1;
    }
    
    static int MapTypeToJob(GatheringPointBase b)
    {
        var type = b.GatheringType.ValueNullable;
        if (type == null) return -1;
        var id = type.Value.RowId;
        return id switch
        {
            0 or 1 or 6 => 8,  // Miner
            2 or 3 or 5 => 9,  // Botanist
            4 or 7      => 10, // Fisher
            _           => -1,
        };
    }
}
