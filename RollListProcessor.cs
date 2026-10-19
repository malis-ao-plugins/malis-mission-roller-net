using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.Inventory;
using AOSharp.Core.UI;
using Newtonsoft.Json;
using SmokeLounge.AOtomation.Messaging.GameData;
using System.Text.Json;

public class ItemEntry
{
    [JsonProperty("ids")] public int[] Ids { get; set; }
    [JsonProperty("ql")] public int Ql { get; set; }
    [JsonProperty("max_ql")] public int MaxQl { get; set; }

    [JsonIgnore] private string? _name;
    public string? Name => _name ??= Item.TryGet(Ids[0], Ids.Length > 1 ? Ids[1] : Ids[0], Ql, out ACGItem item) ? item.Name : null;

    public static bool operator ==(ItemEntry? a, ItemEntry? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Equals(b);
    }

    public static bool operator !=(ItemEntry? a, ItemEntry? b) => !(a == b);

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;

        return obj is ItemEntry other &&
               Ids.SequenceEqual(other.Ids) &&
               Ql == other.Ql;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();

        foreach (var id in Ids)
            hash.Add(id);

        hash.Add(Ql);

        return hash.ToHashCode();
    }
}

public class RollerItemEntry
{
    [JsonProperty("item")] public ItemEntry Item { get; set; }
    [JsonProperty("count")] public int Count { get; set; }
}

public static class RollListProcessor
{
    private const int NanoCrystalTolerance = 10;
    private const int MaxQl = 200;
    private static List<List<int>> _missionLvlsRaw;
    private static List<int> _missionLevels => _missionLvlsRaw[DynelManager.LocalPlayer.Level - 1];

    public static void Init(string pluginDir)
    {
        try
        {
            var path = $"{pluginDir}\\MissionLevels.json";
            var json = File.ReadAllText(path);
            _missionLvlsRaw = JsonConvert.DeserializeObject<List<List<int>>>(json);

        }
        catch (Exception ex)
        {
            Chat.WriteLine($"Deserialize failed: {ex.GetType().Name}");
            Chat.WriteLine(ex.Message);
        }

    }

    public static IEnumerable<(Identity, ItemEntry)> Check(IEnumerable<MissionInfo> missions, IEnumerable<ItemEntry> items)
    {
        return missions
            .Where(mission => items.Any(item => MissionContainsItem(mission, item)))
            .Select(mission => (
                mission.MissionIdentity,
                items.First(item => MissionContainsItem(mission, item))
            ));
    }

    private static bool MissionContainsItem(MissionInfo mission, ItemEntry item)
    {
        return mission.MissionItemData.Any(e => (item.Ids.Contains(e.HighId) || item.Ids.Contains(e.LowId)) && e.Ql == item.Ql) ||
              item.Name != null && mission.Description.Contains(item.Name);
    }

    public static bool TryGetDifficultySliderValue(IEnumerable<ItemEntry> items, out byte value)
    {
        value = 0;
     
        if (!FindNextRollable(items, out var item))
            return false;
      
        var missionLevel = DetermineMissionLevel(item);

        value = (byte)(_missionLevels.IndexOf(missionLevel) + 1);

        return true;
    }

    private static bool FindNextRollable(IEnumerable<ItemEntry> items, out ItemEntry? item)
    {
        item = items.Where(IsRollable).OrderBy(x => x.Ql).FirstOrDefault();

        if (item == null)
            return false;

        return true;
    }

    public static bool HasValidRoll(byte difficulty, IEnumerable<ItemEntry> items)
    {
        return items.Any(x => IsRollable(difficulty, x));
    }

    private static bool IsRollable(byte difficulty, ItemEntry item)
    {
        return IsQlMatch(item, _missionLevels[difficulty], IsNanoCrystal(item.Name));
    }

    private static bool IsRollable(ItemEntry item)
    {
        return _missionLevels.Any(lvl => IsQlMatch(item, lvl, IsNanoCrystal(item.Name)));
    }

    private static int DetermineMissionLevel(ItemEntry item)
    {
        if (item.Ql == MaxQl && item.MaxQl == MaxQl && _missionLevels.Any(lvl => lvl >= MaxQl) && !IsNanoCrystal(item.Name))
            return _missionLevels.First(lvl => lvl >= MaxQl);

        return _missionLevels.OrderBy(lvl => Math.Abs(lvl - item.Ql)).First();
    }

    private static bool IsQlMatch(ItemEntry item, int missionLevel, bool isNanoCrystal)
    {
        return isNanoCrystal ? IsWithinNanoCrystalRange(item.Ql, missionLevel) : item.Ql == missionLevel || item.Ql == MaxQl  && item.MaxQl == MaxQl && missionLevel >= 200;
    }

    private static bool IsWithinNanoCrystalRange(int itemQl, int missionLevel)
    {
        return Math.Abs(itemQl - missionLevel) <= NanoCrystalTolerance;
    }

    private static bool IsNanoCrystal(string? name)
    {
        if (name == null)
            return false;

        return name.Contains("Nano Crystal") || name.Contains("NanoCrystal");
    }
}