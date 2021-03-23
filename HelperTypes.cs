﻿using System;
using System.Collections.Generic;

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
            this.firstFight = fights[0];
            this.secondFight = fights[1];
            this.thirdFight = fights[2];
            this.fourthFight = fights[3];
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
        public float rdps;
        public int kills;
        public int medianPercentile;
        public int highestPercentile;
        public int id;
        public bool savage;
        public Fight part2;
        public Meta meta;

        public static Dictionary<int, string> ShortNameFromId = new Dictionary<int, string>()
            {
                { 73, "e9"}, { 74, "e10" }, { 75, "e11" }, { 76, "e12"}, {77, "e12s p2"}
            };

        public Fight(Meta meta, int encounterId, string job, float rdps, int kills, int medianPercentile, int highestPercentile, bool savage)
        {
            this.id = encounterId;
            this.job = job;
            this.rdps = rdps;
            this.kills = kills;
            this.medianPercentile = medianPercentile;
            this.highestPercentile = highestPercentile;
            this.savage = savage;
            this.meta = meta;
        }

        public Fight(int percent)
        {
            this.highestPercentile = percent;
        }

        public Fight(int encounterId, bool savage)
        {
            this.id = encounterId;
            this.kills = 0;
            this.savage = savage;
        }

        public string getShortName()
        {
            ShortNameFromId.TryGetValue(this.id, out string shortName);
            return shortName + (this.savage ? "s" : "");
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
    }

    public class TEncounter
    {
        public int id { get; set; }
        public string? name { get; set; }
    }
}