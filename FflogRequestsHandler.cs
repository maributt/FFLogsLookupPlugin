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
using Lumina.Excel.GeneratedSheets;

public class FflogRequestsHandler
{

	private string EndpointUrl;
    private FflogsApiCredentials Credentials;
    private string BearerToken = "";

    public RestClient Client;
    public DalamudPluginInterface Interface;

    private List<Lumina.Excel.GeneratedSheets.World> Worlds;
    private List<Lumina.Excel.GeneratedSheets.WorldDCGroupType> WorldDcs;
    private string[] RegionName = new string[4] { "INVALID", "JP", "NA", "EU" };

    private static int[] TwoPartFights = new int[4]
    {
        77,     // Oracle of Darkness
        64,     // The Final Omega
        55,     // God Kefka
        46      // Neo Exdeath
    };

    /// This is in case in the future two part encounters aren't following one another's id
    /*private Dictionary<int, int> TwoPartFights = new Dictionary<int, int>()
    {
        // Oracle of Darkness
        { 77, 76 },

        // The Final Omega
        { 64, 63 },

    };*/


    public FflogRequestsHandler(DalamudPluginInterface pluginInterface, string endpointUrl, FflogsApiCredentials credentials)
	{
		this.EndpointUrl = endpointUrl;
        this.Credentials = credentials;
        this.Interface = pluginInterface;

        this.Worlds =  Interface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.World>().ToList();
        this.WorldDcs = Interface.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.WorldDCGroupType>().ToList();

        this.Client = new RestClient(endpointUrl); //"https://www.fflogs.com/api/v2/client/"

    }

    /// <summary>
    /// Gets the Bearer Token (if it exists) issued by the FFLogs API <br/>
    /// creates a new bearer token otherwise
    /// </summary>
    /// <returns>da token</returns>
    private string GetBearerToken()
    {
        return this.BearerToken ?? "get rekt";
    }


    /*public async Task<RaidingTierPerformance> PerformRequest(FflogsRequestParams parameters, bool summarize=true)
    {
        if (!summarize) return null;
        var response = await PerformRequest(parameters);
        return Summarize(response, parameters.player);
    }*/
    /// <summary>
    /// Queries the FFLogs API on the endpoint passed to the constructor
    /// </summary>
    /// <param name="parameters">Object consisting of a TargetInfo object and extra query parameters</param>
    /// <returns></returns>
    public async Task<FflogsApiResponse> PerformRequest(FflogsRequestParams parameters)
    {
        // TODO: make sure when checking if the inspect window is open that the player name from the window = player name from the request 
        
        var world = this.Worlds.Find(w => w.Name.ToString().Equals(parameters.player.world));
        var dc = this.WorldDcs.Find(d => d.Name.ToString().Equals(world.DataCenter.Value.Name));
        var region = RegionName[dc.Region];

        // allows normal parses to show up if no savage ones are found (grabs the highest diff first (fflogs default))
        var difficultyFilter = parameters.showNormal ? "" : "difficulty: 101,";
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
                    $"\"name\":\"{parameters.player.firstname} {parameters.player.lastname}\"," +
                    $"\"serverSlug\":\"{parameters.player.world}\"," +
                    $"\"serverRegion\":\"{region}\"" +
                "}," +
                "\"operationName\":\"characterData\"" +
            "}";
        var request = new RestRequest(Method.POST)
            .AddHeader("Content-Type", "application/json")
            .AddHeader("Authorization", $"Bearer {GetBearerToken()}")
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
