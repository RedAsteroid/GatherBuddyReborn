using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Inventory;
using Lumina.Excel.Sheets;
using GatherBuddy.Plugin;

namespace GatherBuddy.AutoGather.Collectables;

public static class ItemHelper
{
    public static List<GameInventoryItem> GetCurrentInventoryItems()
    {
        var inventoriesToFetch = new GameInventoryType[]
        {
            GameInventoryType.Inventory1, GameInventoryType.Inventory2, GameInventoryType.Inventory3,
            GameInventoryType.Inventory4
        };
        var inventoryItems = new List<GameInventoryItem>();
        for (int i = 0; i < inventoriesToFetch.Length; i++)
        {
            inventoryItems.AddRange(Dalamud.GameInventory.GetInventoryItems(inventoriesToFetch[i]));
        }
        return inventoryItems;
    }
    
    public static List<Item> GetLuminaItemsFromInventory()
    {
        List<Item> luminaItems = new List<Item>();
        var inventoryItems = GetCurrentInventoryItems();
    
        foreach (var invItem in inventoryItems)
        {
            var luminaItem = Dalamud.GameData.GetExcelSheet<Item>().FirstOrDefault(i => i.RowId == invItem.BaseItemId);
            if (luminaItem.RowId != 0)
                luminaItems.Add(luminaItem);
        }
        return luminaItems;
    }
}
