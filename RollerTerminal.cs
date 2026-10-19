using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.UI;

public class RollerTerminal
{
    public static void Use(byte difficultyValue, RollerSettings settings)
    {
        var closestTerminal = DynelManager.AllDynels
            .Where(x => x.Identity.Type == IdentityType.MissionTerminal)
            .OrderBy(x => x.DistanceFrom(DynelManager.LocalPlayer))
            .FirstOrDefault();

        if (closestTerminal == null)
        {
            Chat.WriteLine("No terminal found");
            return;
        }

        if (closestTerminal.DistanceFrom(DynelManager.LocalPlayer) > 7f)
        {
            Chat.WriteLine("Too far away. Move closer to the terminal");
            return;
        }

        new MissionTerminal(closestTerminal).RequestMissions(
            difficulty: difficultyValue,
            goodBad: Slider(settings.GoodBad),
            orderChaos: Slider(settings.OrderChaos),
            openHidden: Slider(settings.OpenHidden),
            physicalMystical: Slider(settings.PhysicalMystical),
            headonStealth: Slider(settings.HeadonStealth),
            creditsXp: Slider(settings.CreditsXp));
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