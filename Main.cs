using AOSharp.Common.GameData;
using AOSharp.Core;
using AOSharp.Core.Inventory;
using AOSharp.Core.UI;
using Newtonsoft.Json;
using SmokeLounge.AOtomation.Messaging.GameData;

public class Main : AOPluginEntry
{
    private static MissionRoller _roller;
    private static BoundsProcessor _boundsProcessor;
    private static WebSocketServer _webServer;
    private static AOItemQueryService _itemService;
    private static WebPageServer _page;

    public override void Run()
    {
        Chat.WriteLine("Mali's Mission Roller - Net");

        RollListProcessor.Init(PluginDirectory);

        _itemService = new AOItemQueryService(Server.Retail);
        _itemService.Start();
        _roller = new MissionRoller();
        _boundsProcessor = new BoundsProcessor();
        _webServer = new WebSocketServer(7069);
        _webServer.Register("start", _ => _roller.Start());
        _webServer.Register("stop", _ => _roller.Stop());
        _webServer.Register("updateConfig", root =>
        {
            var settings = JsonConvert.DeserializeObject<RollerSettings>(root.GetProperty("settings").GetRawText());
            _roller.Update(settings);
        });

        _webServer.Start();
        _page = new WebPageServer();
        _page.Start();
        _roller.OnItemFound += OnItemFound;
        _roller.OnStopped += () => _webServer.Broadcast(JsonConvert.SerializeObject(new { type = "rolling", rolling = false }));
        Mission.RollListChanged += OnRollListChanged;
    }

    private void OnItemFound(ItemEntry item)
    {
        _webServer.Broadcast(JsonConvert.SerializeObject(new
        {
            type = "missionHit",
            item = new
            {
                ids = item.Ids,
                ql = item.Ql,
            }
        }));
    }

    private static void OnRollListChanged(object sender, RollListChangedArgs e)
    {
        _roller.ResetTick();
        _webServer.Broadcast(SerializeMissions([.. e.MissionDetails]));
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
        _webServer?.Stop();
        _page?.Stop();
        _itemService?.Stop();
        Mission.RollListChanged -= OnRollListChanged;
    }
}