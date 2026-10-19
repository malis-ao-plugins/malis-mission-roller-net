using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.Misc;
using AOSharp.Core.UI;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

public class MissionRoller
{
    private RollerSettings _settings;
    private readonly AutoResetInterval _rollTick = new(1500);
    private List<ItemEntry> _items => _settings.RollList;
    private bool _started = false;
    public Action<ItemEntry> OnItemFound;
    public Action OnStopped;

    public MissionRoller()
    {
    }

    public void Update(RollerSettings settings)
    {
        _settings = settings;
    }

    public bool HasSettings => _settings != null;

    public void Start()
    {
        if (_started)
            return;

        Game.OnUpdate += OnUpdate;
        _started = true;
    }

    public void Stop()
    {
        if (!_started)
            return;

        Game.OnUpdate -= OnUpdate;
        _started = false;
        OnStopped?.Invoke();
    }

    public void ResetTick()
    {
        _rollTick.Reset();
    }

    /// <summary>
    /// Takes the first offered mission that has a wanted item and that the
    /// settings allow. With Auto Accept on it is accepted and rolling goes on;
    /// with it off, rolling stops so it can be accepted by hand.
    /// </summary>
    public void CheckHits(IEnumerable<MissionInfo> missions)
    {
        if (_settings == null)
            return;

        var allowed = missions.Where(m => MissionFilter.Allows(m, _settings));
        var hits = RollListProcessor.Check(allowed, _items);
        if (!hits.Any()) return;

        var (missionId, item) = hits.First();
        OnItemFound?.Invoke(item);

        if (_settings.AutoAcceptMissions)
        {
            Network.Send(new CreateQuestMessage { MissionId = missionId });
            return;
        }

        Stop();
        Chat.WriteLine($"Match found: {item.Name ?? "a wanted item"}. Auto Accept is off, so accept it at the terminal.");
    }

    private void OnUpdate(object sender, float e)
    {
        if (!_rollTick.Elapsed || _settings == null)
            return;

        byte difficultyValue;

        // When rolling cannot get anywhere, say why once and stop, rather than repeating it every roll.
        if (_settings.AutoAdjustLvlSlider)
        {
            if (!RollListProcessor.TryGetDifficultySliderValue(_items, out difficultyValue))
            {
                Chat.WriteLine("Mission Roller: nothing in the roll list can come from a mission at your level. Rolling stopped.");
                Stop();
                return;
            }
        }
        else
        {
            if (!RollListProcessor.HasValidRoll((byte)_settings.EasyHard, _items))
            {
                Chat.WriteLine("Mission Roller: nothing in the roll list can come from a mission at this slider position. Rolling stopped.");
                Stop();
                return;
            }

            difficultyValue = (byte)_settings.EasyHard;
        }

        if (!RollerTerminal.Use(difficultyValue, _settings))
            Stop();
    }
}

public class BoundsProcessor
{
    private Vector3 _startPos = Vector3.Zero;
    private Vector3 _endPos = Vector3.Zero;

    public void Start()
    {
        _endPos = Vector3.Zero;
        _startPos = DynelManager.LocalPlayer.Position;
        Game.OnUpdate += OnUpdate;
    }

    public void Stop(out Vector3 startPos, out Vector3 endPos)
    {
        _endPos = DynelManager.LocalPlayer.Position;
        startPos = _startPos;
        endPos = _endPos;
        Game.OnUpdate -= OnUpdate;
    }

    private void OnUpdate(object sender, float deltaTime)
    {
        DrawBounds(_startPos, DynelManager.LocalPlayer.Position, new Vector3(0, 1, 0));
    }

    public void DrawBounds(Vector3 start, Vector3 end, Vector3 color)
    {
        float y = Math.Min(start.Y, end.Y);

        Vector3 a = new(start.X, y, start.Z);
        Vector3 b = new(end.X, y, start.Z);
        Vector3 c = new(end.X, y, end.Z);
        Vector3 d = new(start.X, y, end.Z);

        Debug.DrawLine(a, b, color);
        Debug.DrawLine(b, c, color);
        Debug.DrawLine(c, d, color);
        Debug.DrawLine(d, a, color);
    }
}