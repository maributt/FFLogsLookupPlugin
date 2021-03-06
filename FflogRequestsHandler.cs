using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HelperTypes;
using Dalamud.Plugin;
using System.Linq;
using Dalamud.Interface;
using FFLogsLookup;

public class FflogRequestsHandler
{
    private string BearerToken;
    
    public RestClient Client;
    public RestClient OAuthClient;
    public DalamudPluginInterface Interface;

    public string AccessTokenUrl = "https://www.fflogs.com/oauth/token";
    public string ClientEndpoint = "https://www.fflogs.com/api/v2/client/";

    public const string LOGS_NOT_FOUND = "Logs not found.";
    public const string HIDDEN_LOGS = "Hidden logs.";
    public const string DownForMaintenance = "The site is offline for a brief maintenance.";

    public static List<Lumina.Excel.GeneratedSheets.World> _worlds;
    public static List<Lumina.Excel.GeneratedSheets.WorldDCGroupType> _worldDcs;
    public static string[] _regionName = new string[4] { "INVALID", "JP", "NA", "EU" };

    private static int[] TwoPartFights = new int[]
    {
        77,     // Oracle of Darkness
        64,     // The Final Omega
        55,     // God Kefka
        46,     // Neo Exdeath

        // for the time being just storing the second part's ID is fine
        // that's because the fight's first part's id is one less than the second part's
    };

    private Configuration config;

    public FflogRequestsHandler(DalamudPluginInterface pluginInterface, Configuration config)
	{
        this.Interface = pluginInterface;
        this.config = config;
        this.BearerToken = string.IsNullOrWhiteSpace(config.bearer_token) ? GetBearerToken().Result : config.bearer_token;
        if (!config.initialConfig)
        {
            if (string.IsNullOrWhiteSpace(config.client_id) || string.IsNullOrWhiteSpace(config.client_secret))
            {
                config.initialConfig = true;
            }
        }

        _worlds =  Interface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.World>().ToList();
        _worldDcs = Interface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.WorldDCGroupType>().ToList();

        this.Client = new RestClient(ClientEndpoint);
        this.OAuthClient = new RestClient(AccessTokenUrl);
    }

    

    /// <summary>
    /// Gets the Bearer Token (if it exists) as issued by the FFLogs API <br/>
    /// requests a new bearer token from the API otherwise
    /// </summary>
    /// <returns>da token</returns>
    private async Task<string> GetBearerToken(bool forceRefreshBearer=false)
    {
        if (this.BearerToken != null && !forceRefreshBearer)
            return this.BearerToken;
        var request = new RestRequest() {Method = Method.POST};
        // i found this on stackoverflow lolololo
        request.AddHeader("cache-control", "no-cache");
        request.AddHeader("content-type", "application/x-www-form-urlencoded");
        request.AddParameter(
            "application/x-www-form-urlencoded", 
            "grant_type=client_credentials" +
            $"&client_id={config.client_id}" +
            $"&client_secret={config.client_secret}", 
            ParameterType.RequestBody
        );
        var response = await OAuthClient.ExecuteAsync(request);
        //PluginLog.Log(response.Content);
        var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content);
        if (!obj.Keys.Contains("access_token")) return "";
        this.BearerToken = (string)obj["access_token"];
        config.bearer_token = this.BearerToken;
        config.Save();
        return this.BearerToken;
    }
    
    private async Task<string> GetBearerToken(string client_id, string client_secret)
    {
        var request = new RestRequest() {Method = Method.POST};
        // i found this on stackoverflow lolololo
        request.AddHeader("cache-control", "no-cache");
        request.AddHeader("content-type", "application/x-www-form-urlencoded");
        request.AddParameter(
            "application/x-www-form-urlencoded", 
            "grant_type=client_credentials" +
            $"&client_id={client_id}" +
            $"&client_secret={client_secret}", 
            ParameterType.RequestBody
        );
        var response = await OAuthClient.ExecuteAsync(request);
        if (response.Content == DownForMaintenance) return null;
        var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content);
        if (!obj.Keys.Contains("access_token")) return "";
        this.BearerToken = (string)obj["access_token"];
        config.bearer_token = this.BearerToken;
        config.Save();
        return this.BearerToken;
    }

    /// <summary>
    /// Test a pair of client id and client secret by asking the API
    /// </summary>
    /// <param name="client_id"></param>
    /// <param name="client_secret"></param>
    /// <returns>Whether or not the id/secret pair was valid</returns>
    public async Task<int> TestRequest(string client_id, string client_secret)
    {
        var request = new RestRequest(Method.POST);
        var bearer = await GetBearerToken(client_id, client_secret);
        switch (bearer)
        {
            case null:
                return 2;
            case "":
                return 0;
        }
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Authorization", $"Bearer {bearer}");
        request.AddParameter("application/json", "{\"query\":\"query {\\n  rateLimitData {\\n    pointsResetIn\\n  }\\n}\"}", ParameterType.RequestBody);
        var response = await this.Client.ExecuteAsync(request);
        PluginLog.Log("test req: "+response.Content);
        try
        {
            var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content);
            return obj.Keys.Contains("error") ? 0 : 1;
        }
        catch (Exception)
        {
            this.BearerToken = bearer;
            return 1;
        }
    }

    public async Task<dynamic> PerformUltRequest(PluginUi.InspectInfo iInfo)
    {
        
        var world = _worlds.Find(w => w.Name.ToString().ToLower().Equals(iInfo.HomeWorld.ToLower()));
        var dc = _worldDcs.Find(d => d.Name.ToString().Equals(world.DataCenter.Value.Name));
        var region = _regionName[dc.Region];

        var prettifiedReq = "{\"query\":" +
                            "\"query characterData {\\n" +
                            "  characterData {\\n" +
                            "    character(name: \\\""+iInfo.FirstName+" "+iInfo.LastName+"\\\", serverSlug: \\\""+iInfo.HomeWorld+"\\\", serverRegion: \\\""+region+"\\\") {\\n" +
                            "      hidden\\n" +
                            (iInfo.ReqTea ? "      Tea: zoneRankings(zoneID: 32)\\n" : "") +
                            (iInfo.ReqUcob||iInfo.ReqUwu ? "      UcobUwuNew: zoneRankings(zoneID: 30)\\n" : "")+
                            (iInfo.ReqUcob ? "      UcobOld: zoneRankings(zoneID: 19)\\n" : "")+
                            (iInfo.ReqUwu ? "      UwuOld: zoneRankings(zoneID: 23)\\n": "") +
                            "    }\\n" +
                            "  }\\n" +
                            "}\\n\"," +
                            "\"operationName\":\"characterData\"" +
                            "}";

        var request = new RestRequest(Method.POST)
            .AddHeader("Content-Type", "application/json")
            .AddHeader("Authorization", $"Bearer {await GetBearerToken()}")
            .AddParameter("application/json", prettifiedReq, ParameterType.RequestBody);
        
        var cancellationTokenSource = new CancellationTokenSource();
        var response = await Client.ExecuteAsync(request, cancellationTokenSource.Token);
        PluginLog.Log(response.Content);
        return JsonConvert.DeserializeObject(response.Content);
        
    }
    
    public async Task<FflogsApiResponse> PerformRequest(TargetInfo player)
    {
        // TODO: make sure when checking if the inspect window is open that the player name from the window = player name from the request 
        
        var world = _worlds.Find(w => w.Name.ToString().ToLower().Equals(player.world.ToLower()));
        var dc = _worldDcs.Find(d => d.Name.ToString().Equals(world.DataCenter.Value.Name));
        var region = _regionName[dc.Region];

        // allows normal parses to show up if no savage ones are found (grabs the highest diff first (fflogs default))
        var difficultyFilter = "";
        // this is if you are actually demented just straight up crazy:
        difficultyFilter = (this.config.ShowOnlyNormal ?  "difficulty: 100," : difficultyFilter);
        var zoneid = config.CurrentDisplayZoneID;
        if (ConfigUI.SavageZoneIDs.Contains(config.CurrentDisplayZoneID))
        {
            difficultyFilter = config.ShowNormal ? "" : "difficulty: 101,";
        } else if (ConfigUI.SavageZoneNoDiffFilterIDs.Contains(config.CurrentDisplayZoneID))
        {
            zoneid -= config.ShowNormal ? 1 : 0;
        }

        var prettifiedReq =
            "{" +
                "\"query\" : \"query characterData($name: String!, $serverSlug: String!, $serverRegion: String!) " +
                    "{" +
                        "\\n  characterData {" +
                        "   \\n    character(name: $name, serverSlug: $serverSlug, serverRegion: $serverRegion) {" +
                            "   \\n      hidden" +
                            $"   \\n      raidingTierData: zoneRankings({difficultyFilter} zoneID:{zoneid})" +
                            "\\n    }" +
                        "\\n  }" +
                    "\\n}" +
                "\\n\"," +
                "\"variables\":{" +
                    $"\"name\":\"{player.firstname} {player.lastname}\"," +
                    $"\"serverSlug\":\"{player.world}\"," +
                    $"\"serverRegion\":\"{region}\"" +
                "}," +
                "\"operationName\":\"characterData\"" +
            "}";
        var request = new RestRequest(Method.POST)
            .AddHeader("Content-Type", "application/json")
            .AddHeader("Authorization", $"Bearer {await GetBearerToken()}")
            .AddParameter("application/json", prettifiedReq, ParameterType.RequestBody);

        var cancellationTokenSource = new CancellationTokenSource();
        var response = await Client.ExecuteAsync(request, cancellationTokenSource.Token);
        /*PluginLog.Log($"encoding: {response.ContentEncoding}" +
                      $"type: {response.ContentType}" +
                      $"errormsg: {response.ErrorMessage}" +
                      $"statuscode: {response.StatusCode}");*/
        if (response.Content == DownForMaintenance) return null;
        return JsonConvert.DeserializeObject<FflogsApiResponse>(response.Content);
    }

    /// <summary>
    /// Looks up the first part of a two part encounter and returning its id if it exists<br/>
    /// (this assumes the id of the second part is one more than its first part)
    /// </summary>
    /// <param name="fightId">the supposed two part fight</param>
    /// <param name="firstPartId">the first part's id of the two part fight</param>
    /// <returns>True if fight has a first part</returns>
    public static bool GetFirstPartForFight(int fightId, out int firstPartId) 
    {
        firstPartId = fightId;
        if (FflogRequestsHandler.TwoPartFights.Contains(fightId))
        {
            firstPartId -= 1;
            return true;
        }
        return false;
    }

    public List<Fight> SummarizeUlt(dynamic ults, PluginUi.InspectInfo iInfo, out bool correct)
    {
        correct = false; // GUILTY UNTIL PROVEN INNOCENT
        var ultimates = new List<Fight>();
        var meta = new Meta {erroredProcessing = false};
        foreach (var fight in new List<dynamic>()
        {
            ults.data.characterData.character?.Tea,
            ults.data.characterData.character?.UcobUwuNew,
            ults.data.characterData.character?.UcobOld,
            ults.data.characterData.character?.UwuOld,
        })
        {
            PluginLog.Log("is fight null = "+(fight==null));
            if (fight != null)
                foreach (var entry in fight?.rankings)
                {
                    PluginLog.Log("has player cleared the fight? "+((int)entry.totalKills == 0));
                    /*var f = new Fight(entry.encounter.id, false, false);
                    if (entry.totalKills != 0) 
                        f = new Fight(
                            meta,
                            entry.encounter.name,
                            entry.encounter.id,
                            entry.bestSpec,
                            entry.bestAmount,
                            entry.totalKills,
                            (int)Math.Floor(entry.medianPercent ?? -1),
                            (int)Math.Floor(entry.rankPercent  ?? -1),
                            false, false
                        );
                    PluginLog.Log(f.ToString());
                    ultimates.Add(f);*/
                }   
        }

        return ultimates;
    }
    
    /// <summary>
    /// Summarizes a `FflogsApiResponse` and `TargetInfo` object into a more easily exploitable one systematically exposing 4 fight entries. 
    /// </summary>
    /// <param name="response">The FflogApiResponse object</param>
    /// <param name="target"></param>
    /// <returns></returns>
    public RaidingTierPerformance Summarize(FflogsApiResponse response)
    {
        var meta = new Meta {erroredProcessing = false};

        if (response.data.characterData.character == null)
        {
            //meta.hoverText = ;
            meta.hoverText = LOGS_NOT_FOUND;
            meta.longHoverText =
                $"Couldn't find logs for _";
            meta.icon = FontAwesomeIcon.QuestionCircle.ToIconString();
            meta.erroredProcessing = true;
            return new RaidingTierPerformance(new Fight[4], meta);

        }
        if (response.data.characterData.character.hidden)
        {
            meta.hoverText = HIDDEN_LOGS;
            meta.longHoverText = $"_'s logs are hidden.";
            meta.icon = FontAwesomeIcon.EyeSlash.ToIconString();
            meta.erroredProcessing = true;
            return new RaidingTierPerformance(new Fight[4], meta);
        }

        // adjust the fights array size based on whether or not some fights are twoparters
        var twoparters =
            response.data.characterData.character.raidingTierData.rankings.Count(ranking => TwoPartFights.Contains(ranking.encounter.id));
        var fights = new Fight[response.data.characterData.character.raidingTierData.rankings.Length-twoparters];
        var index = 0;

        foreach (var ranking in response.data.characterData.character.raidingTierData.rankings)
        {
            Fight processedFight = null;
            var isSavage =
                ConfigUI.SavageZoneNoDiffFilterIDs.Contains(response.data.characterData.character.raidingTierData.zone); // the zone doesn't have a difficulty field if true
            var isExtreme = ConfigUI.ExTrialIDs.Contains(response.data.characterData.character.raidingTierData.zone);
            if (ranking.totalKills == 0)
            {
                processedFight = new Fight(ranking.encounter.id, (response.data.characterData.character.raidingTierData?.difficulty == 101 || isSavage), isExtreme);
            }
            else
            {
                processedFight = new Fight(
                    new Meta(ranking.encounter.name),
                    ranking.encounter.name,
                    ranking.encounter.id,
                    ranking.bestSpec,
                    ranking.bestAmount,
                    ranking.totalKills,
                    (int)Math.Floor(ranking.medianPercent ?? 0),
                    (int)Math.Floor(ranking.rankPercent ?? 0),
                    (response.data.characterData.character.raidingTierData?.difficulty == 101 || isSavage),
                    isExtreme
                    );
            }

            if (GetFirstPartForFight(ranking.encounter.id, out var firstPartId))
            {
                var appendTo = Array.Find(fights, fight => fight.id == firstPartId);
                if (appendTo != null) appendTo.part2 = processedFight;
            }
            else
            {
                fights[index] = processedFight;
            }
            index++;
        }

        return new RaidingTierPerformance(fights);

    }
}
