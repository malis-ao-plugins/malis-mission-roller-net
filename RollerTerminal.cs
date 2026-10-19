using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.UI;

public class RollerTerminal
{
    /// <summary>Asks the nearest terminal for missions. False, with the reason in chat, when there is none in reach.</summary>
    public static bool Use(byte difficultyValue, RollerSettings settings)
    {
        var closestTerminal = DynelManager.AllDynels
            .Where(x => x.Identity.Type == IdentityType.MissionTerminal)
            .OrderBy(x => x.DistanceFrom(DynelManager.LocalPlayer))
            .FirstOrDefault();

        if (closestTerminal == null)
        {
            Chat.WriteLine("Mission Roller: no mission terminal nearby. Rolling stopped.");
            return false;
        }

        if (closestTerminal.DistanceFrom(DynelManager.LocalPlayer) > 7f)
        {
            Chat.WriteLine("Mission Roller: too far from the terminal. Move closer and roll again.");
            return false;
        }

        new MissionTerminal(closestTerminal).RequestMissions(
            difficulty: difficultyValue,
            goodBad: Slider(settings.GoodBad),
            orderChaos: Slider(settings.OrderChaos),
            openHidden: Slider(settings.OpenHidden),
            physicalMystical: Slider(settings.PhysicalMystical),
            headonStealth: Slider(settings.HeadonStealth),
            creditsXp: Slider(settings.CreditsXp));
        return true;
    }

    /// <summary>
    /// The terminal takes a side slider as a signed byte from -100 to 100. A
    /// centred slider is sent as 255, the terminal's own "no preference".
    /// </summary>
    private static byte Slider(float value)
    {
        int v = Math.Clamp((int)Math.Round(value), -100, 100);
        return v == 0 ? (byte)255 : unchecked((byte)(sbyte)v);
    }
}