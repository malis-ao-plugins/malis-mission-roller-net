using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.Inventory;
using AOSharp.Core.UI;
using Newtonsoft.Json;
using SmokeLounge.AOtomation.Messaging.GameData;
using SmokeLounge.AOtomation.Messaging.Messages.N3Messages;

public class Main : AOPluginEntry
{
    private static MissionRoller _roller;
    private static BoundsProcessor _boundsProcessor;
    private static ToolkitApp _toolkit;
    private static AOItemQueryService _itemService;
    private static MissionInfo[] _offered = [];
    private static volatile int _acceptRequested;
    private static volatile bool _startRequested;
    private static volatile bool _stopRequested;
    private static volatile RollerSettings? _incomingSettings;

    public override void Run()
    {
        Chat.WriteLine("Mali's Mission Roller - Net");

        RollListProcessor.Init(PluginDirectory);

        _itemService = new AOItemQueryService(Server.Retail);
        _itemService.Start();
        _roller = new MissionRoller();
        _boundsProcessor = new BoundsProcessor();
        _toolkit = ToolkitHub.Register("app_7069");
        // Socket handlers run off the game thread, so they only record what the window asked for;
        // OnUpdate acts on it on the game thread.
        _toolkit.Register("start", _ => _startRequested = true);
        _toolkit.Register("stop", _ => _stopRequested = true);
        _toolkit.Register("accept", root => _acceptRequested = root.GetProperty("id").GetInt32());
        _toolkit.Register("updateConfig", root =>
            _incomingSettings = JsonConvert.DeserializeObject<RollerSettings>(root.GetProperty("settings").GetRawText()));

        // A reloaded window cannot know the roller is still running unless it is told.
        _toolkit.PageConnected += () =>
            _toolkit.Send(JsonConvert.SerializeObject(new { type = "rolling", rolling = _roller.IsRolling }));

        _roller.OnItemFound += OnItemFound;
        _roller.OnStopped += () => _toolkit.Send(JsonConvert.SerializeObject(new { type = "rolling", rolling = false }));
        Mission.RollListChanged += OnRollListChanged;
        Game.OnUpdate += OnUpdate;
    }

    /// <summary>Carries out what the window asked for: new settings, start, stop and accept.</summary>
    private static void OnUpdate(object sender, float deltaTime)
    {
        var settings = _incomingSettings;
        if (settings != null)
        {
            _incomingSettings = null;
            _roller.Update(settings);
        }

        if (_stopRequested)
        {
            _stopRequested = false;
            _startRequested = false;
            _roller.Stop();
        }

        if (_startRequested)
        {
            _startRequested = false;
            if (_roller.HasSettings)
            {
                _roller.Start();
                Chat.WriteLine("Mission Roller: rolling.");
            }
            else
            {
                Chat.WriteLine("Mission Roller: no settings from the window yet. Reload the toolkit page, then roll again.");
                _toolkit.Send(JsonConvert.SerializeObject(new { type = "rolling", rolling = false }));
            }
        }

        int id = _acceptRequested;
        if (id == 0)
            return;

        _acceptRequested = 0;
        var mission = _offered.FirstOrDefault(m => m.MissionIdentity.Instance == id);
        if (mission == null)
        {
            Chat.WriteLine("That mission is no longer offered. Roll again and accept from the new list.");
            return;
        }

        Network.Send(new CreateQuestMessage { MissionId = mission.MissionIdentity });
    }

    private void OnItemFound(ItemEntry item)
    {
        _toolkit.Send(JsonConvert.SerializeObject(new
        {
            type = "missionHit",
            item = new
            {
                ids = item.Ids,
                ql = item.Ql,
            },
            // The list as the plugin now has it, with the hit already taken off;
            // the window shows this rather than working the change out itself.
            rollList = _roller.RollList,
        }));
    }

    private static void OnRollListChanged(object sender, RollListChangedArgs e)
    {
        _roller.ResetTick();
        _offered = [.. e.MissionDetails];
        _toolkit.Send(SerializeMissions([.. e.MissionDetails]));
        _roller.CheckHits(e.MissionDetails);
    }

    private static string SerializeMissions(List<MissionInfo> missions)
    {
        var list = missions.Select(m => new
        {
            icon = m.MissionIcon,
            id = m.MissionIdentity.Instance,
            name = m.Title,
            playfield = m.Playfield.Instance.ToString(),
            credits = m.Credits,
            creditsMax = m.Credits,
            xp = m.XpReward,
            description = m.Description,
            rewards = m.MissionItemData?.Select(r => new
            {
                ids = new[] { r.LowId, r.HighId },
                ql = r.Ql,
            }).ToArray() ?? Array.Empty<object>(),
        });

        return JsonConvert.SerializeObject(new
        {
            type = "missionResults",
            missions = list,
        });
    }

    public override void Teardown()
    {
        _roller?.Stop();
        _toolkit?.Dispose();
        _itemService?.Stop();
        Mission.RollListChanged -= OnRollListChanged;
        Game.OnUpdate -= OnUpdate;
    }
}