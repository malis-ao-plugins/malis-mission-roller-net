using AOSharp.Common.GameData;
using AOSharp.Core.UI;
using Newtonsoft.Json;
using System.Text.Json;

public class RollerSettings
{
    [JsonProperty("easyHard")] public float EasyHard { get; set; }
    [JsonProperty("goodBad")] public float GoodBad { get; set; } 
    [JsonProperty("orderChaos")] public float OrderChaos { get; set; }
    [JsonProperty("openHidden")] public float OpenHidden { get; set; }
    [JsonProperty("physicalMystical")] public float PhysicalMystical { get; set; } 
    [JsonProperty("headonStealth")] public float HeadonStealth { get; set; } 
    [JsonProperty("creditsXp")] public float CreditsXp { get; set; } 

    [JsonProperty("returnItem")] public bool ReturnItem { get; set; } 
    [JsonProperty("killTarget")] public bool KillTarget { get; set; } 
    [JsonProperty("useItem")] public bool UseItem { get; set; }
    [JsonProperty("findItem")] public bool FindItem { get; set; } 

    [JsonProperty("autoAdjustLvlSlider")] public bool AutoAdjustLvlSlider { get; set; }
    [JsonProperty("removeRolledEntries")] public bool RemoveRolledEntries { get; set; } 
    [JsonProperty("autoAcceptMissions")] public bool AutoAcceptMissions { get; set; } 

    // Milliseconds between terminal requests.
    [JsonProperty("rollInterval")] public int RollInterval { get; set; } = 1500;

    [JsonProperty("locations")]
    public Dictionary<int, LocationFilter> Locations { get; set; }

    [JsonProperty("rollList")]
    public List<ItemEntry> RollList { get; set; } = new();

    public void ToggleMissionType(string label)
    {
        switch (label)
        {
            case "Return Item": ReturnItem = !ReturnItem; break;
            case "Kill Target": KillTarget = !KillTarget; break;
            case "Use Item": UseItem = !UseItem; break;
            case "Find Item": FindItem = !FindItem; break;
        }
    }

    public void ToggleExtra(string label)
    {
        switch (label)
        {
            case "Auto Adjust Lvl Slider": AutoAdjustLvlSlider = !AutoAdjustLvlSlider; break;
            case "Remove Rolled Entries": RemoveRolledEntries = !RemoveRolledEntries; break;
            case "Auto Accept Missions": AutoAcceptMissions = !AutoAcceptMissions; break;
        }
    }

    public void SetSlider(int index, float value)
    {
        switch (index)
        {
            case 0: EasyHard = value; break;
            case 1: GoodBad = value; break;
            case 2: OrderChaos = value; break;
            case 3: OpenHidden = value; break;
            case 4: PhysicalMystical = value; break;
            case 5: HeadonStealth = value; break;
            case 6: CreditsXp = value; break;
        }
    }

    public void TogglePlayfield(int pfId)
    {
        if (!Locations.TryGetValue(pfId, out var loc)) return;
        Locations[pfId] = loc with { Active = !loc.Active };
    }
    public void EnablePlayfield(int pfId, bool state)
    {
        if (!Locations.TryGetValue(pfId, out var loc)) return;
        Locations[pfId] = loc with { Active = state };
    }

    public void RemoveBounds(int pfId)
    {
        if (!Locations.TryGetValue(pfId, out var loc)) return;
        Locations[pfId] = new LocationFilter { Active = loc.Active, X1 = 0, Y1 = 0, X2 = 0, Y2 = 0 };
    }

    public void UpdateLocation(int pfId, int x1, int y1, int x2, int y2)
    {
        var existing = Locations.TryGetValue(pfId, out var loc) ? loc : new LocationFilter();
        Locations[pfId] = new LocationFilter
        {
            Active = existing.Active,
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
        };
    }

    public void UpdateLocation(int pfId, Vector3 startPos, Vector3 endPos)
    {
        UpdateLocation(pfId, (int)startPos.X, (int)startPos.Z, (int)endPos.X, (int)endPos.Z);
    }
}

public record LocationFilter
{
    [JsonProperty("active")] public bool Active { get; init; }
    [JsonProperty("x1")] public int X1 { get; init; }
    [JsonProperty("y1")] public int Y1 { get; init; }
    [JsonProperty("x2")] public int X2 { get; init; }
    [JsonProperty("y2")] public int Y2 { get; init; }

    public bool Contains(int x, int y) =>
        x >= Math.Min(X1, X2) && x <= Math.Max(X1, X2) &&
        y >= Math.Min(Y1, Y2) && y <= Math.Max(Y1, Y2);
}