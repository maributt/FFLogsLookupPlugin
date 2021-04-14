using System;
using System.Collections.Generic;
using FFLogsLookup;

namespace HelperTypes
{

    public class FflogsApiCredentials
    {
        public string id = null;
        public string secret = null;
        public string bearer_token;

        public FflogsApiCredentials(string id, string secret)
        {
            this.id = id;
            this.secret = secret;
        }

        public FflogsApiCredentials()
        {
            
        }
        
        public FflogsApiCredentials(string bearerToken)
        {
            this.bearer_token = bearerToken;
        }
    }

    public class Meta
    {
        public string hoverText;
        public string longHoverText;
        public string icon = "";
        public bool erroredProcessing = false;

        public Meta(string hoverText)
        {
            this.hoverText = hoverText;
        }

        public Meta() { }
    }

    public class RaidingTierPerformance
    {
        public Fight firstFight;
        public Fight secondFight;
        public Fight thirdFight;
        public Fight fourthFight;
        public Fight[] fightsArray;
        public Meta meta = null;

        public RaidingTierPerformance(Fight first, Fight second, Fight third, Fight fourth)
        {
            this.firstFight = first;
            this.secondFight = second;
            this.thirdFight = third;
            this.fourthFight = fourth;
            this.fightsArray = new Fight[] { first, second, third, fourth };
        }

        public RaidingTierPerformance(Fight[] fights)
        {
            if (fights.Length >= 1) this.firstFight = fights[0];
            if (fights.Length >= 2) this.secondFight = fights[1];
            if (fights.Length >= 3) this.thirdFight = fights[2];
            if (fights.Length >= 4) this.fourthFight = fights[3];
            this.fightsArray = fights;
        }

        public RaidingTierPerformance(Fight[] fights, Meta meta)
        {
            this.firstFight = fights[0];
            this.secondFight = fights[1];
            this.thirdFight = fights[2];
            this.fourthFight = fights[3];
            this.fightsArray = fights;
            this.meta = meta;
        }

        public RaidingTierPerformance(int percentiles)
        {
            this.fightsArray = new Fight[]
                {new Fight(percentiles), new Fight(percentiles), new Fight(percentiles), new Fight(percentiles) {part2 = new Fight(percentiles)}};
        }
    }

    public class Fight
    {
        public string job;
        public string bossname;
        public float rdps;
        public int kills;
        public int medianPercentile;
        public int highestPercentile;
        public int id;
        public bool savage;
        public bool extreme;
        public Fight part2;
        public Meta meta;

        public static Dictionary<int, string> ShortNameFromId = new Dictionary<int, string>()
            {
                // Shadowbringers
                /* Promise */ { 73, "e9" }, { 74, "e10" }, { 75, "e11" }, { 76, "e12"}, {77, "e12s p2"},
                /* Verse   */ { 69, "e5" }, { 70, "e6" }, { 71, "e7" }, { 72, "e8"},
                /* Gate    */ { 65, "e1" }, { 66, "e2" }, { 67, "e3" }, { 68, "e4"},

                // Stormblood
                /* Alphascape */ { 60, "o9" }, { 61, "o10" }, { 62, "o11" }, { 63, "o12" }, { 64, "o12s p2" }, 
                /* Sigmascape */ { 51, "o5" }, { 52, "o6" }, { 53, "o7" }, { 54, "o8" }, { 55, "o8s p2" }, 
                /* Deltascape */ { 42, "o1" }, { 43, "o2" }, { 44, "o3" }, { 45, "o4" }, { 46, "o4s p2" }, 
            };

        public Fight(Meta meta, string name, int encounterId, string job, float rdps, int kills, int medianPercentile, int highestPercentile, bool savage, bool extreme)
        {
            this.bossname = name;
            this.id = encounterId;
            this.job = job;
            this.rdps = rdps;
            this.kills = kills;
            this.medianPercentile = medianPercentile;
            this.highestPercentile = highestPercentile;
            this.savage = savage;
            this.meta = meta;
            this.extreme = extreme;
        }

        public Fight(int percent)
        {
            this.highestPercentile = percent;
            this.savage = true;
            this.extreme = false;
        }

        public Fight(int encounterId, bool savage, bool extreme)
        {
            this.id = encounterId;
            this.kills = 0;
            this.savage = savage;
            this.extreme = extreme;
        }

        public string getShortName()
        {
            ShortNameFromId.TryGetValue(this.id, out string shortName);
            if (!FflogRequestsHandler.GetFirstPartForFight(this.id, out _))
                shortName += (this.savage ? "s" : "");
            return shortName == "" ? this.bossname : shortName;
        }

        public int getHighestAvgForParts()
        {
            int avg = -1;
            if (this.part2 != null)
            {
                avg = (int)((highestPercentile + this.part2.highestPercentile) / 2);
            }
            return avg;
        }

        public override string ToString()
        {
            return $"{this.getShortName()} - {this.bossname} : {this.job} ({this.kills}) {this.highestPercentile}/{this.medianPercentile}";
        }
    }

    public class TargetInfo
    {
        public string firstname;
        public string lastname;
        public string world;

        public TargetInfo(string firstname, string lastname, string world)
        {

            var strings = new string[] { firstname, lastname, world };
            /*strings = strings.Select(s =>
            {
                char[] a = s.ToCharArray();
                a[0] = char.ToUpper(a[0]);
                return new string(a);
            }).ToArray();*/

            this.firstname = strings[0];
            this.lastname = strings[1];
            this.world = strings[2];
        }

        public TargetInfo() { }

        public override string ToString()
        {

            return $"{this.firstname} {this.lastname} ({this.world})";
        }
    }

    public class FflogsRequestParams
    {
        public TargetInfo player;
        public bool showNormal;
        public bool showUltimates;

        public FflogsRequestParams(TargetInfo player, bool showNormal, bool showUltimates)
        {
            this.player = player;
            this.showNormal = showNormal;
            this.showUltimates = showUltimates;
        }
        // 32 tea, 30 ucob uwu (shb), 23 uwu og, 19 ucob og (zone id
        // 1050 tea, 1048 uwu, 1047 ucob, 1042 uwu og, 1039 ucob og (encounter id
    }


    public class FflogsApiResponse
    {
        public TData data { get; set; }
        public class TData
        {
            public TCharacterData characterData { get; set; }
            public class TCharacterData
            {
                public TCharacter character { get; set; }
            }
        }
    }

    public class UltFflogsApiResponse
    {
        public TData data { get; set; }
        public class TData
        {
            public TCharacterData characterData { get; set; }
            public class TCharacterData
            {
                public TUltCharacter ultCharacter { get; set; }
                public override string ToString()
                {
                    return "(characterData: " + ultCharacter + " )";
                }
            }

            public override string ToString()
            {
                return "( data: " + this.characterData + " )";
            }
        }

        public override string ToString()
        {
            return "(" + this.data + ")";
        }
    }
    public class TUltCharacter
    {
        public bool hidden { get; set; }
        public TRaidingTierData? ultimate1 { get; set; }
        public TRaidingTierData? ultimate2 { get; set; }
        public TRaidingTierData? ultimate3 { get; set; }
        public TRaidingTierData? ultimate4 { get; set; }
        public override string ToString()
        {
            var ult = "";
            foreach (var rtd in new List<TRaidingTierData>() {ultimate1, ultimate2, ultimate3, ultimate4})
            {
                if (rtd != null) ult += rtd.ToString();
            }
            return "(character: \n hidden: "+hidden+"\n "+ult+" )";
        }
    }

    public class TCharacter
    {
        public bool hidden { get; set; }
        public TRaidingTierData raidingTierData { get; set; }
    }

    public class TRaidingTierData
    {
        public float? bestPerformanceAverage { get; set; }
        public float? medianPerformanceAverage { get; set; }
        public int difficulty { get; set; }
        public string metric { get; set; }
        public int partition { get; set; }
        public int zone { get; set; }
        public TAllStar[] allStars { get; set; }
        public TRanking[] rankings { get; set; }
        public override string ToString()
        {
            var encnames = "";
            foreach (var enc in rankings)
            {
                encnames += " " + enc.encounter.name;
            }
            return ""+encnames;
        }
    }

#nullable enable
    public class TAllStar
    {
        public int? partition { get; set; }
        public string? spec { get; set; }
        public float? points { get; set; }
        public int? possiblePoint { get; set; }
        public int? rank { get; set; }
        public float? rankPercent { get; set; }
        public int? total { get; set; }

    }


    public class TRanking
    {
        public TEncounter? encounter { get; set; }
        public float? rankPercent { get; set; }
        public float? medianPercent { get; set; }
        public int totalKills { get; set; }
        public int fastestKill { get; set; }
        public string? spec { get; set; }
        public string? bestSpec { get; set; }
        public float bestAmount { get; set; }

        public override string ToString()
        {
            return $"( encID:{encounter?.id}, encName:{encounter?.name} ) - ( hi:{rankPercent}, med:{medianPercent} ) - (totalKills:{totalKills}, bestSpec:{bestSpec})";
        }
    }

    public class TEncounter
    {
        public int id { get; set; }
        public string? name { get; set; }
    }
}
