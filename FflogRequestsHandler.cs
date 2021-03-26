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
using Lumina.Excel.GeneratedSheets;
using RestSharp.Authenticators;

public class FflogRequestsHandler
{
    private string BearerToken;
    
    public RestClient Client;
    public RestClient OAuthClient;
    public DalamudPluginInterface Interface;

    public string AccessTokenUrl = "https://www.fflogs.com/oauth/token";
    public string ClientEndpoint = "https://www.fflogs.com/api/v2/client/";

    private readonly List<Lumina.Excel.GeneratedSheets.World> _worlds;
    private readonly List<Lumina.Excel.GeneratedSheets.WorldDCGroupType> _worldDcs;
    private readonly string[] _regionName = new string[4] { "INVALID", "JP", "NA", "EU" };

    private static int[] TwoPartFights = new int[4]
    {
        77,     // Oracle of Darkness
        64,     // The Final Omega
        55,     // God Kefka
        46      // Neo Exdeath
        // for the time being just storing the second part's ID is fine
        // that's because the fight's first part's id is one less than the second part's
    };

    private Configuration config;

    public FflogRequestsHandler(DalamudPluginInterface pluginInterface, Configuration config)
	{
        this.Interface = pluginInterface;
        this.config = config;
        this.BearerToken = config.bearer_token;

        this._worlds =  Interface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.World>().ToList();
        this._worldDcs = Interface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.WorldDCGroupType>().ToList();

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
        var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content);
        /*foreach (var key in obj.Keys)
        {
            PluginLog.Log("found key: "+key);
            PluginLog.Log("value: "+obj[key]);
        }*/
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
        var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content);
        /*foreach (var key in obj.Keys)
        {
            PluginLog.Log("found key: "+key);
            PluginLog.Log("value: "+obj[key]);
        }*/
        if (!obj.Keys.Contains("access_token")) return "";
        this.BearerToken = (string)obj["access_token"];
        config.bearer_token = this.BearerToken;
        config.Save();
        return this.BearerToken;
    }

    public async Task<bool> TestRequest(string client_id, string client_secret)
    {
        var request = new RestRequest(Method.POST);
        var bearer = await GetBearerToken(client_id, client_secret);
        if (bearer == "")
        {
            return false;
        } 
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Authorization", $"Bearer {bearer}");
        request.AddParameter("application/json", "{\"query\":\"query {\\n  rateLimitData {\\n    pointsResetIn\\n  }\\n}\"}", ParameterType.RequestBody);
        var response = await this.Client.ExecuteAsync(request);
        try
        {
            var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content);
            return !obj.Keys.Contains("error");
        }
        catch (Exception e)
        {
            this.BearerToken = bearer;
            return true;
        }
    }
    
    public async Task<FflogsApiResponse> PerformRequest(TargetInfo player)
    {
        // TODO: make sure when checking if the inspect window is open that the player name from the window = player name from the request 
        
        var world = this._worlds.Find(w => w.Name.ToString().Equals(player.world));
        var dc = this._worldDcs.Find(d => d.Name.ToString().Equals(world.DataCenter.Value.Name));
        var region = _regionName[dc.Region];

        // allows normal parses to show up if no savage ones are found (grabs the highest diff first (fflogs default))
        var difficultyFilter = this.config.ShowNormal ? "" : "difficulty: 101,";
        // this is if you are actually demented just straight up crazy:
        difficultyFilter = (this.config.ShowOnlyNormal ?  "difficulty: 100" : difficultyFilter); 
 
        var prettifiedReq =
            "{" +
                "\"query\" : \"query characterData($name: String!, $serverSlug: String!, $serverRegion: String!) " +
                    "{" +
                        "\\n  characterData {" +
                        "   \\n    character(name: $name, serverSlug: $serverSlug, serverRegion: $serverRegion) {" +
                            "   \\n      hidden" +
                            $"   \\n      raidingTierData: zoneRankings({difficultyFilter} metric: rdps)" +
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

    /// <summary>
    /// Summarizes a `FflogsApiResponse` and `TargetInfo` object into a more easily exploitable one systematically exposing 4 fight entries. 
    /// </summary>
    /// <param name="response">The FflogApiResponse object</param>
    /// <param name="target"></param>
    /// <returns></returns>
    public RaidingTierPerformance Summarize(FflogsApiResponse response, TargetInfo target)
    {
        var meta = new Meta {erroredProcessing = false};

        if (response.data.characterData.character == null)
        {
            //meta.hoverText = $"Couldn't find logs for a character by the name \"{target.firstname} {target.lastname}\" on \"{target.world}\".";
            meta.hoverText = "Logs not found.";
            meta.icon = FontAwesomeIcon.QuestionCircle.ToIconString();
            meta.erroredProcessing = true;
            return new RaidingTierPerformance(new Fight[4], meta);

        }
        if (response.data.characterData.character.hidden)
        {
            //meta.hoverText = "Character's logs are hidden. (sus)";
            meta.hoverText = "Hidden logs.";
            meta.icon = FontAwesomeIcon.EyeSlash.ToIconString();
            meta.erroredProcessing = true;
            return new RaidingTierPerformance(new Fight[4], meta);
        }

        var fights = new Fight[4];
        var index = 0;

        foreach (var ranking in response.data.characterData.character.raidingTierData.rankings)
        {
            Fight processedFight = null;
            if (ranking.totalKills == 0)
            {
                processedFight = new Fight(ranking.encounter.id, response.data.characterData.character.raidingTierData.difficulty == 101);
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
                    response.data.characterData.character.raidingTierData.difficulty == 101
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
