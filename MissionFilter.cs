using SmokeLounge.AOtomation.Messaging.GameData;

/// <summary>
/// Whether a mission the terminal offered is one the settings allow: its type
/// is enabled, its zone is enabled, and it lies inside that zone's bounds when
/// bounds are set.
/// </summary>
public static class MissionFilter
{
    // Mission types are told apart by their icon.
    private const int ReturnItemIcon = 11329;
    private const int KillTargetIcon = 11330;
    private const int FindItemIcon = 11337;
    private const int UseItemIcon = 11342;

    public static bool Allows(MissionInfo mission, RollerSettings settings) =>
        TypeAllowed(mission.MissionIcon, settings) && LocationAllowed(mission, settings);

    private static bool TypeAllowed(int icon, RollerSettings settings) => icon switch
    {
        ReturnItemIcon => settings.ReturnItem,
        KillTargetIcon => settings.KillTarget,
        FindItemIcon => settings.FindItem,
        UseItemIcon => settings.UseItem,
        _ => true,
    };

    /// <summary>
    /// A zone missing from the list counts as disabled. Bounds with no area
    /// mean the whole zone.
    /// </summary>
    private static bool LocationAllowed(MissionInfo mission, RollerSettings settings)
    {
        if (settings.Locations == null || settings.Locations.Count == 0)
            return true;

        if (!settings.Locations.TryGetValue(mission.Playfield.Instance, out var zone) || !zone.Active)
            return false;

        bool hasBounds = zone.X1 != zone.X2 && zone.Y1 != zone.Y2;
        return !hasBounds || zone.Contains((int)mission.Location.X, (int)mission.Location.Z);
    }
}
