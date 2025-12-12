using System.Linq;
using System.Text.Json.Serialization;
using Dalamud.Interface.Textures;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using NewtonsoftJsonIgnore = Newtonsoft.Json.JsonIgnoreAttribute;

namespace GatherBuddy.AutoGather.Collectables.Data;

public class ScripShopItem
{
    [JsonIgnore]
    [NewtonsoftJsonIgnore]
    public string Name => Item.Name.ToString();
    
    [JsonPropertyName("ItemId")]
    public uint ItemID { get; set; }
    public int Index { get; set; }
    public uint ItemCost { get; set; }
    public int Page { get; set; }
    public int SubPage { get; set; }
    public ScripType ScripType { get; set; }
    
    [JsonIgnore]
    [NewtonsoftJsonIgnore]
    private Item? _itemCache;
    [JsonIgnore]
    [NewtonsoftJsonIgnore]
    public Item Item => _itemCache ??= Svc.Data.GetExcelSheet<Item>().GetRow(ItemID);
    [JsonIgnore]
    [NewtonsoftJsonIgnore]
    public uint ItemId => ItemID;
    [JsonIgnore]
    [NewtonsoftJsonIgnore]
    private ISharedImmediateTexture? _iconTextureCache;
    [JsonIgnore]
    [NewtonsoftJsonIgnore]
    public ISharedImmediateTexture IconTexture => _iconTextureCache ??= Svc.Texture.GetFromGameIcon(new GameIconLookup((uint)Item.Icon));
}
