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
        
        private enum ChatColors
        {
            Yellow = 548,
            Pink = 561,
            Orange = 540,
            Purple = 541,
            Blue = 37,
            Green = 45,
            Grey = 4
        }
        private List<string> TargetPlaceholders = new List<string>()
        {
            "<t>", "<mo>", "<f>", "<me>"
        };
        private ConfigUI configUi;
        public string Name => "FFLogs Lookup";

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
            this.config = (Configuration)this.pluginInterface.GetPluginConfig() ?? new Configuration();
            this.fflog = new FflogRequestsHandler(this.pluginInterface, this.config);
            this.Chat = pluginInterface.Framework.Gui.Chat;
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

        [Command("/ffll")]
        [HelpMessage("Lookup a given character's FFlogs parses")]
        public async void LookupChatCommand(string command, string arguments)
        {
            
            if (config.initialConfig)
            {
                Chat.PrintError("You must complete the initial configuration process before you can start looking up players' logs!");
                return;
            }
            var args = arguments.Split(' ');

            // incorrect n of args
            // will add a config ui when called with 0 arguments in the future
            if (args.Length == 1 && args[0] == "")
            {
                this.configUi.IsVisible = !this.configUi.IsVisible;
                return;
            }
            
            if (args.Length > 3)
            {
                Chat.PrintError("An invalid number of arguments was passed!");
                return;
            }

            // if any argument is ""
            if (args.Any(s => s==""))
            {
                Chat.PrintError(
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

            // if the first argument is a placeholder
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

                // if there are 2 arguments (including placeholder) the second argument is the world
                if (args.Length == 2) targetInfo.world = args[1];

            }
            
            else if (args.Length == 1)
            {
                Chat.PrintError($"{args[0]} is not a valid selector.");
                return;
            }
            
            // if 2 or 3 arguments are passed (first, last) || (first, last, world)
            else if (args.Length >= 2)
            {
                targetInfo.firstname = args[0];
                targetInfo.lastname = args[1];
                if (args.Length == 3)
                {
                    //loosely check if valid world to not waste API quota

                    targetInfo.world = args[2];
                }
            }

            FflogsApiResponse response = await fflog.PerformRequest(targetInfo);
            var tierSummary = fflog.Summarize(response, targetInfo);
            
            // if an "error" occurred during Summarize (character not found, hidden logs)
            if (tierSummary.meta.erroredProcessing)
            {
                Chat.PrintError(tierSummary.meta.hoverText);
                return;
            }

            // ready payloads
            var tierEntries = new List<Payload>() { 
                new TextPayload($"\n{targetInfo}\n")
            };

            foreach (var fight in tierSummary.fightsArray)
            {
                
                // ready the fight entry
                var encounterEntry = new List<Payload>()
                {
                    new UIForegroundPayload(this.pluginInterface.Data, (ushort)GetColorFromPercentile(fight.highestPercentile)),
                    new TextPayload( "   " + (fight.highestPercentile==0?"-":fight.highestPercentile+"")),
                    new UIForegroundPayload(this.pluginInterface.Data, 0)
                };

                // if fight has a second part, tack it onto the initial entry to display together
                if (fight.part2 != null)
                {
                    encounterEntry.Concat(new List<Payload>()
                    {
                        new TextPayload("/"),
                        new UIForegroundPayload(this.pluginInterface.Data, (ushort)GetColorFromPercentile(fight.part2.highestPercentile)),
                        new TextPayload("" + (fight.part2.highestPercentile==0?"-":fight.part2.highestPercentile+"")),
                        new UIForegroundPayload(this.pluginInterface.Data, 0)
                    });
                }

                // add fight's entry to the whole tier's entry
                tierEntries = tierEntries.Concat(encounterEntry).ToList();
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
