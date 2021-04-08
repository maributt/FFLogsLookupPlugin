using Dalamud.Plugin;
using FFLogsLookup.Attributes;
using System;
using Dalamud.Game.ClientState.Actors.Types;
using System.Collections.Generic;
using RestSharp;
using Dalamud.Game.Chat;
using Dalamud.Game.Chat.SeStringHandling;
using SeString = Dalamud.Game.Chat.SeStringHandling.SeString;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using System.Linq;
using HelperTypes;
using System.IO;

namespace FFLogsLookup
{
    public class Plugin : IDalamudPlugin
    {
        private DalamudPluginInterface pluginInterface;
        private PluginCommandManager<Plugin> commandManager;
        private Configuration config;
        private PluginUi ui;
        private FflogRequestsHandler fflog;
        private Dalamud.Game.Internal.Gui.ChatGui Chat;

        public static int LATEST_RAID_ID = 38;  
        private enum ChatColors
        {
            Yellow = 25,
            Pink = 561,
            Orange = 540,
            Purple = 541,
            Blue = 37,
            Green = 45,
            Grey = 4,
            
            None = 0,
            Error = 540,
            LightGrey = 3,
        }
        private List<string> TargetPlaceholders = new List<string>()
        {
            "<t>", "<mo>", "<f>", "<me>"
        };
        private ConfigUI configUi;
        public string Name => "FFLogs Lookup";

        public void Initialize(DalamudPluginInterface PluginInterface)
        {
            this.pluginInterface = PluginInterface;
            this.config = (Configuration)this.pluginInterface.GetPluginConfig() ?? new Configuration();
            this.fflog = new FflogRequestsHandler(this.pluginInterface, this.config);
            this.Chat = PluginInterface.Framework.Gui.Chat;
            this.config.Initialize(this.pluginInterface);
            this.ui = new PluginUi(this.pluginInterface, this.fflog, this.config);
            this.configUi = new ConfigUI(this.pluginInterface, this.config, this.fflog);
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.Draw;
            this.pluginInterface.UiBuilder.OnBuildUi += this.configUi.Draw;
            this.commandManager = new PluginCommandManager<Plugin>(this, this.pluginInterface);
            if (config.initialConfig)
            {
                this.pluginInterface.UiBuilder.OnBuildUi += this.configUi.DrawInitialSetup;
            }

            this.pluginInterface.UiBuilder.OnOpenConfigUi += ShowConfigWindow;

        }

        private void ShowConfigWindow(object sender, EventArgs args)
        {
            this.configUi.IsVisible = true;
        }
        
        /// <summary>
        /// Retrieves the actor object matching the chat placeholder used (&lt;mo&gt;, etc)
        /// </summary>
        /// <param name="placeholder"></param>
        /// <returns></returns>
        public Actor TargetObjFromPlaceholder(string placeholder)
        {
            var target = pluginInterface.ClientState.Targets;
            switch (placeholder)
            {
                case "<t>":
                    return target.CurrentTarget;

                case "<mo>":
                    return target.MouseOverTarget;

                case "<f>":
                    return target.FocusTarget;

                case "<me>":
                    return pluginInterface.ClientState.LocalPlayer;

                default:
                    // never reached
                    return null;
            };
        }

        /// <summary>
        /// Get color from a passed percentile matching as closely as possible fflogs' usual color scheme
        /// </summary>
        /// <param name="percentile"></param>
        /// <returns></returns>
        public int GetColorFromPercentile(int percentile)
        {
            if (percentile < 25) return (int)ChatColors.Grey;
            if (percentile < 50) return (int)ChatColors.Green;
            if (percentile < 75) return (int)ChatColors.Blue;
            if (percentile < 95) return (int)ChatColors.Purple;
            if (percentile < 99) return (int)ChatColors.Orange;
            if (percentile < 100) return (int)ChatColors.Pink;
            return (int)ChatColors.Yellow;
        }

        public void printWarning(string msg)
        {
            Chat.PrintChat(new XivChatEntry()
            {
                MessageBytes = new SeString(new List<Payload>()
                {
                    new IconPayload(BitmapFontIcon.Warning),
                    new UIForegroundPayload(this.pluginInterface.Data, (ushort)ChatColors.Error),
                    new TextPayload(msg),
                    new UIForegroundPayload(this.pluginInterface.Data, (ushort) ChatColors.None)
                }).Encode()
            });
        }

        public void printWarning(string before, TargetInfo target, string after)
        {
            Chat.PrintChat(new XivChatEntry()
            {
                MessageBytes = new SeString(new List<Payload>()
                {
                    new IconPayload(BitmapFontIcon.Warning),
                    new UIForegroundPayload(this.pluginInterface.Data, (ushort)ChatColors.Error),
                    new TextPayload(before),
                    new TextPayload($"{target.firstname} {target.lastname}"),
                    new IconPayload(BitmapFontIcon.CrossWorld),
                    new TextPayload(target.world),
                    new TextPayload(after),
                    new UIForegroundPayload(this.pluginInterface.Data, (ushort) ChatColors.None)
                }).Encode()
            });
        }

        [Command("/ffll")]
        [HelpMessage("Lookup a given character's FFlogs parses\n" +
                     "Example:\n" +
                     "     /ffll first_name last_name world (if world isn't specified it assumes the character is on your world)\n" +
                     "     /ffll <target selector> world (valid target selectors are: <mo>, <t>, <me>, <f>) (world is assumed if not mentioned)\n"+
                     "     /ffll -> opens the configuration interface"
        )]
        public async void LookupChatCommand(string command, string arguments)
        {
            
            if (config.initialConfig)
            {
                printWarning("You must complete the initial configuration process before you can start looking up players' logs!");
                return;
            }
            var args = arguments.Split(' ');

            
            if (args.Length == 1 && args[0] == "")
            {
                this.configUi.IsVisible = !this.configUi.IsVisible;
                return;
            }
            
            // incorrect number of arguments
            if (args.Length > 3)
            {
                printWarning("An invalid number of arguments was passed!");
                return;
            }

            // if any argument is ""
            if (args.Any(s => s=="") || args[0] == "?" || args[0].ToLower() == "help") 
            {
                printWarning(
                    $"{command} : Lookup a given character's FFlogs parses" +
                    $"\n   - {command} [{string.Join(",",TargetPlaceholders)}] [server]" +
                    $"\n   - {command} <first name> <last name> [server]" +
                    $"\nIf not specified, server will default to your current world: " +
                    $"{this.pluginInterface.ClientState.LocalPlayer.CurrentWorld.GameData.Name.RawString}"
                    );
                return;
            }

            var targetInfo = new TargetInfo
            {
                world = this.pluginInterface.ClientState.LocalPlayer.CurrentWorld.GameData.Name.RawString
            };

            // if the first argument is a <target selector>
            if (TargetPlaceholders.Contains(args[0]))
            {
                var target = TargetObjFromPlaceholder(args[0]);
                if (target == null)
                {
                    Chat.PrintError($"{args[0]} was used but no target was found.");
                    return;
                }
                var targetFullName = target.Name.Split(' ');
                targetInfo.firstname = targetFullName[0];
                targetInfo.lastname = targetFullName[1];

                // if there are 2 arguments total (including <target selector>) the second argument is the world
                if (args.Length == 2) targetInfo.world = args[1];

            }
            
            else if (args.Length == 1)
            {
                printWarning($"{args[0]} is not a valid selector.");
                return;
            }
            
            // if 2 or 3 arguments are passed (first, last) || (first, last, world)
            else if (args.Length >= 2)
            {
                targetInfo.firstname = args[0];
                targetInfo.lastname = args[1];
                if (args.Length == 3)
                {
                    //check if valid world to not waste API quota
                    targetInfo.world = args[2];
                    if (FflogRequestsHandler._worlds.Find(w => w.Name.ToString().ToLower().Equals(targetInfo.world.ToLower())) == null)
                    {
                        printWarning($"\"{targetInfo.world}\" is not a valid world name.");
                        return;
                    }
                }
            }
            
            FflogsApiResponse response = await fflog.PerformRequest(targetInfo);
            var tierSummary = fflog.Summarize(response, targetInfo);
            var fString = "";
            
            // if an "error" occurred during Summarize (character not found, hidden logs)
            if ((tierSummary?.meta?.erroredProcessing ?? false))
            {
                var errStr = tierSummary.meta.longHoverText.Split('_');
                printWarning(errStr[0], targetInfo, errStr[1]);
                return;
            }

            // ready payloads
            var tierEntries = new List<Payload>() { 
                new TextPayload("\n"),
                new TextPayload(targetInfo.ToString()),
                new TextPayload("\n")
            };

            foreach (var fight in tierSummary.fightsArray)
            {
                // ready the fight chat entry
                var entryStr = "   " + (fight.highestPercentile == 0 ? "-" : fight.highestPercentile + "");
                var encounterEntry = new List<Payload>()
                {
                    new UIForegroundPayload(this.pluginInterface.Data, (ushort)GetColorFromPercentile(fight.highestPercentile)),
                    new TextPayload( entryStr ),
                    new UIForegroundPayload(this.pluginInterface.Data, 0)
                };
                fString += entryStr;
                
                // if fight has a second part, tack it onto the initial entry to display together
                if (fight.part2 != null)
                {
                    
                    var p2entryStr =
                        "" + (fight.part2.highestPercentile == 0 ? "-" : fight.part2.highestPercentile + "");
                    fString += "/" + p2entryStr;
                    encounterEntry = encounterEntry.Concat(new List<Payload>()
                    {
                        new TextPayload("/"),
                        new UIForegroundPayload(this.pluginInterface.Data, (ushort) GetColorFromPercentile(fight.part2.highestPercentile)),
                        new TextPayload(p2entryStr),
                        new UIForegroundPayload(this.pluginInterface.Data, (ushort) ChatColors.None)
                    }).ToList();
                }

                // add fight's entry to the whole tier's entry
                tierEntries = tierEntries.Concat(encounterEntry).ToList();
            }

            var headerParsesLenDiff = targetInfo.ToString().Length - fString.Length;
            var headerIndex = 1;
            var parsesIndex = 4;
            var targetPayload = headerParsesLenDiff > 0 ? parsesIndex : headerIndex;
            var paddingAmount = Math.Abs(headerParsesLenDiff / 2);
            ((TextPayload) tierEntries[targetPayload]).Text = new string(' ', paddingAmount) + ((TextPayload) tierEntries[targetPayload]).Text;

            var bottomText = ""; // :joy: :100:
            
            // check first fight to see if the parses were logged on the savage diff or normal
            if (config.ShowTierName || !tierSummary.firstFight.savage)
            {
                tierEntries.Add(new TextPayload("\n"));
                
                var tierName = ConfigUI.zones[config.CurrentDisplayZoneID].name +
                               (!tierSummary.firstFight.savage ? " " : "");
                var diffName = !tierSummary.firstFight.savage ? "(Normal)" : "";

                bottomText += tierName + diffName;

                if (config.ShowTierName) {
                    tierEntries = tierEntries.Concat( new List<Payload>()
                    {
                        new UIForegroundPayload(this.pluginInterface.Data, (ushort) ChatColors.LightGrey),
                        new TextPayload(tierName),
                        new UIForegroundPayload(this.pluginInterface.Data, (ushort) ChatColors.None)
                    }).ToList();
                    var btHeaderLenDiff = bottomText.Length - (((TextPayload) tierEntries[headerIndex]).Text.Length);
                    var btParsesLenDiff = bottomText.Length - (((TextPayload) tierEntries[parsesIndex]).Text.Length-paddingAmount);
                    if (btHeaderLenDiff > 0) // bottom text is larger than header
                    {
                        if (btParsesLenDiff > 0) // bottom text is also larger than parses, align header + parses to bottom text
                        {
                            
                            PluginLog.Log("align header and parses to bottom text");
                            ((TextPayload) tierEntries[headerIndex]).Text = new string(' ', (int)btHeaderLenDiff) +
                                                                            ((TextPayload) tierEntries[headerIndex])
                                                                            .Text;
                            ((TextPayload) tierEntries[parsesIndex]).Text = new string(' ', btParsesLenDiff / 2) +
                                                                            ((TextPayload) tierEntries[parsesIndex])
                                                                            .Text;
                        }
                        else // parses is the largest string, align bottom text to parses
                        {
                            PluginLog.Log("align bottom text to parses");
                            ((TextPayload) tierEntries[tierEntries.Count - 2]).Text = 
                                new string(' ', Math.Abs(btParsesLenDiff/2)) + 
                                ((TextPayload) tierEntries[tierEntries.Count - 2]).Text;
                        }
                    }
                    else // align bottom text to header
                    {
                        PluginLog.Log("align bottom text to header");
                        ((TextPayload) tierEntries[tierEntries.Count - 2]).Text = 
                            new string(' ', Math.Abs(btHeaderLenDiff/2)) + 
                            ((TextPayload) tierEntries[tierEntries.Count - 2]).Text;
                    }
                    
                }
                if (!tierSummary.firstFight.savage) {
                    tierEntries = tierEntries.Concat(new List<Payload>()
                    {
                        new UIForegroundPayload(this.pluginInterface.Data, (ushort) ChatColors.LightGrey),
                        new TextPayload(diffName),
                        new UIForegroundPayload(this.pluginInterface.Data, (ushort) ChatColors.None)
                    }).ToList();
                    if (!config.ShowTierName)
                    {
                        var btHeaderLenDiff = bottomText.Length - (((TextPayload) tierEntries[headerIndex]).Text.Length);
                        var btParsesLenDiff = bottomText.Length - (((TextPayload) tierEntries[parsesIndex]).Text.Length-paddingAmount);
                        if (btHeaderLenDiff > 0) // bottom text is larger than header
                        {
                            if (btParsesLenDiff > 0) // bottom text is also larger than parses, align header + parses to bottom text
                            {
                                
                                PluginLog.Log("align header and parses to bottom text");
                                ((TextPayload) tierEntries[headerIndex]).Text = new string(' ', (int)btHeaderLenDiff) +
                                                                                ((TextPayload) tierEntries[headerIndex])
                                                                                .Text;
                                ((TextPayload) tierEntries[parsesIndex]).Text = new string(' ', btParsesLenDiff / 2) +
                                                                                ((TextPayload) tierEntries[parsesIndex])
                                                                                .Text;
                            }
                            else // parses is the largest string, align bottom text to parses
                            {
                                PluginLog.Log("align bottom text to parses");
                                ((TextPayload) tierEntries[tierEntries.Count - 2]).Text = 
                                    new string(' ', Math.Abs(btParsesLenDiff/2)) + 
                                    ((TextPayload) tierEntries[tierEntries.Count - 2]).Text;
                            }
                        }
                        else // align bottom text to header
                        {
                            PluginLog.Log("align bottom text to header");
                            ((TextPayload) tierEntries[tierEntries.Count - 2]).Text = 
                                new string(' ', Math.Abs(btHeaderLenDiff)) + 
                                ((TextPayload) tierEntries[tierEntries.Count - 2]).Text;
                        }
                    }
                    
                    
                }
                
                
            }
            
            // post the result
            Chat.PrintChat(new XivChatEntry
            {
                MessageBytes = new SeString(tierEntries).Encode()
            });
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.commandManager.Dispose();
            this.pluginInterface.SavePluginConfig(this.config);
            this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.Draw;
            this.pluginInterface.UiBuilder.OnBuildUi -= this.configUi.Draw;
            if (config.initialConfig)
            {
                this.pluginInterface.UiBuilder.OnBuildUi -= this.configUi.DrawInitialSetup;
            }
            this.pluginInterface.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
