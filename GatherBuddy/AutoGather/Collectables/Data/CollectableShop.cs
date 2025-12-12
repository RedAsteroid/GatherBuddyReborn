using System.Numerics;
using System.Text.Json.Serialization;

namespace GatherBuddy.AutoGather.Collectables.Data;

public class CollectableShop
{
    public string Name { get; set; } = string.Empty;
    public Vector3 Location { get; set; }
    public uint AetheryteId { get; set; }
    public uint TerritoryId { get; set; }
    public uint NpcId { get; set; }
    public uint ScripShopNpcId { get; set; }
    public bool Disabled { get; set; } = false;
    public bool IsLifestreamRequired { get; set; } = false;
    public string LifestreamCommand { get; set; } = "";
    private Vector3? _scripShopLocation;
    
    [JsonPropertyName("ScripShopLocation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Vector3 ScripShopLocation
    {
        get => _scripShopLocation ?? Location;
        set => _scripShopLocation = value;
    }
}
