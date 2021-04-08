using ImGuiNET;
using Dalamud.Plugin;
using FFXIVClientStructs.Component.GUI;
using System.Runtime.InteropServices;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Dalamud.Interface;
using HelperTypes;

namespace FFLogsLookup
{
    public class PluginUi
    {
        public bool IsVisible { get; set; }

        private readonly DalamudPluginInterface _interface;
        private readonly FflogRequestsHandler _fflog;
        private readonly InspectInfo _target;
        private readonly Lumina.Excel.ExcelSheet<Lumina.Excel.GeneratedSheets.Title> _titles;
        private readonly Configuration _config;
        private bool _requestOnce;
        //private bool _requestUltOnce;

        private List<string> _ultimateTitles = new List<string>()
        {
            "The Legend",
            "The Ultimate Legend",
            "The Perfect Legend",
            "The Litgend:joy::joy::joy::joy::joy::joy::joy::joy::joy::joy::100:"
        };

        public static readonly Vector4 Yellow = new Vector4(0.898f, 0.8f, 0.501f, 1f);
        public static readonly Vector4 Pink = new Vector4(0.886f, 0.408f, 0.659f, 1f);
        public static readonly Vector4 Orange = new Vector4(1.0f, 0.5019f, 0.0f, 1.0f);
        public static readonly Vector4 Purple = new Vector4(0.639f, 0.2078f, 0.933f, 1f);
        public static readonly Vector4 Blue = new Vector4(0f, 0.439f, 1f, 1f);
        public static readonly Vector4 Green = new Vector4(0.117f, 1f, 0f, 1f);
        public static readonly Vector4 Grey = new Vector4(0.4f, 0.4f, 0.4f, 1f);

        private RaidingTierPerformance RaidingPerformance { get; set; }
        
        public PluginUi(DalamudPluginInterface pluginInterface, FflogRequestsHandler fflog, Configuration config)
        {
            this._interface = pluginInterface;
            this._fflog = fflog;
            this._config = config;
            this._target = new InspectInfo();
            // ughhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh
            // load the lumina sheet for titles in whatever language the client is
            // ideally i would pass this: (Lumina.Data.Language)((int)Interface.ClientState.ClientLanguage)
            // as an argument but play the game in english please and thank you
            this._titles = _interface.Data.Excel.GetSheet<Lumina.Excel.GeneratedSheets.Title>();
            this._requestOnce = true;
            this.RaidingPerformance = new RaidingTierPerformance(0);
            //this._requestUltOnce = true;
        }

        private void FormatFightEntryForImGui(Fight f)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, GetColorFromPercentile(f.highestPercentile));
            ImGui.Text(""+( f.highestPercentile switch
            {
                0 => "-",
                100 => "★",
                _ => f.highestPercentile
            }));
            ImGui.PopStyleColor();
            if (!ImGui.IsItemHovered()) return;
            ImGui.BeginTooltip();
            ImGui.Text(f.meta.hoverText ?? $"{f.getShortName()} ({f.job}) (kills: {f.kills})");
            ImGui.EndTooltip();
        }
        
        /// <summary>
        /// Checks whether or not the addon InspectWindow is currently visible and populates this.Target accordingly
        /// </summary>
        /// <returns></returns>
        private unsafe bool IsCharacterInspectVisible()
        {
            
            var inspectWindow = (AtkUnitBase*)_interface.Framework.Gui.GetUiObjectByName("CharacterInspect", 1);
            if (inspectWindow == null || !inspectWindow->IsVisible) return false;
            
            try
            {
                this._target.winPosX = inspectWindow->X;
                this._target.winPosY = inspectWindow->Y;
                this._target.winHeight = inspectWindow->RootNode->Height;
                this._target.winWidth = inspectWindow->RootNode->Width;
                this._target.winScale = inspectWindow->Scale;

                this._target.offsetX = (87) * (this._target.winScale);
                this._target.offsetY = this._target.winHeight +
                                       (float) (
                                           (-50 * Math.Pow(this._target.winScale, 2f)) +
                                           595 * this._target.winScale
                                           - 616);
                var inspectWindowNodeList = inspectWindow->ULDData.NodeList;
                this._target.homeWorld = Marshal.PtrToStringAnsi(new IntPtr(((AtkTextNode*)inspectWindowNodeList[59])->NodeText.StringPtr));
                var name = "";
                
                var titleCandidate1 = (AtkTextNode*)inspectWindowNodeList[61];
                var titleCandidate2 = (AtkTextNode*)inspectWindowNodeList[62];

                // if title isn't hidden
                // (aka if a title is set since the name will always be in the 61st nodelist element otherwise)
                if (titleCandidate1->AtkResNode.IsVisible)
                {
                    // i mainly don't wanna deal with other languages
                    // because converting strings that aren't ainsi scares me
                    var possibleTitle = Marshal.PtrToStringAnsi(new IntPtr(titleCandidate1->NodeText.StringPtr));
                    
                    // shoutouts to people who name themselves after a title
                    var isTitle = _titles.ToList().Find(entry => 
                        entry.Masculine == possibleTitle 
                        || entry.Feminine == possibleTitle
                        ) != null;
                    
                    if (isTitle)
                    {
                        this._target.title = possibleTitle;
                        name = Marshal.PtrToStringAnsi(new IntPtr(titleCandidate2->NodeText.StringPtr));
                    }
                    
                    else
                    {
                        name = possibleTitle;
                        this._target.title = Marshal.PtrToStringAnsi(new IntPtr(titleCandidate2->NodeText.StringPtr));
                    }
                }
                else
                {
                    name = Marshal.PtrToStringAnsi(new IntPtr(titleCandidate2->NodeText.StringPtr));
                }

                if (name == null) return false; // basically impossible but my IDE wont stop screaming at me unless i put this here
                
                var aName = name.Split(' ');
                this._target.firstName = aName[0];
                this._target.lastName = aName[1];
                return true;
            } catch (Exception) 
                // ideally you wouldn't try/catch here but there is a specific scenario (right after first loading into a zone, before inspecting anyone) that can occur
                // where if you inspect someone the window will take longer than it usually would to truly display (a bit like the initial laoding time on the saddlebag but a lot less obvious to the player's eye)
            {
                return false;
            }
            

            // got a bit of an issue here already identified: whenever the client zones into a new area and first tries to examine someone an error is raised
            // this possibly has to do with how the data is loaded the first time a character inspection is requested (kind of like how the saddlebag's content's request is performed)

            
        }

        private void RequestAgain()
        {
            // TODO: make the tier parses be requested again whenever the tier is changed so that the character inspect window doesn't have to be reopened after changing stuff
            return;
        }

        /// <summary>
        /// Returns a color to dye the percentile text with (according to fflogs' website's colors) 
        /// </summary>
        /// <param name="percentile"></param>
        /// <returns></returns>
        private Vector4 GetColorFromPercentile(int percentile)
        {
            if (percentile < 25) return Grey;
            if (percentile < 50) return Green;
            if (percentile < 75) return Blue;
            if (percentile < 95) return Purple;
            if (percentile < 99) return Orange;
            return percentile < 100 ? Pink : Yellow;
        }

        public bool CharacterInspectItemTooltipVisible()
        {
            var tooltip = _interface.Framework.Gui.GetAddonByName("Tooltip", 1);
            if (tooltip.Visible) return false;

            var itemdetail = _interface.Framework.Gui.GetAddonByName("ItemDetail", 1);
            if (itemdetail.X < this._target.winPosX + this._target.offsetX + this._config.OffsetX)
            {
                if (itemdetail.Y + itemdetail.Height - 30 >
                    this._target.winPosY + this._target.offsetY + this._config.OffsetY)
                {
                    return itemdetail.Visible;
                }
            }
            return false;

        }

        public async void Draw()
        {
            // if the character inspect window is not visible we make sure fflogs data is requested when it next is
            
            if (!IsCharacterInspectVisible())
            {
                this._requestOnce = true;
                this.RaidingPerformance = new RaidingTierPerformance(0);
                return;
            }

            if (CharacterInspectItemTooltipVisible())
            {
                return;
            }
            
            if (_config.initialConfig)
            {
                ImGui.SetNextWindowPos(
                    new Vector2(
                        this._target.winPosX +10,
                        this._target.winPosY + this._target.winHeight -65
                    )
                );
                ImGui.Begin("reminder",ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize);
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0f, 0f, 1f));
                ImGui.Text("You must complete the initial configuration\nbefore you can start looking at other players' logs!");
                ImGui.PopStyleColor();
                ImGui.End();
                return;
            }

            /*if (_config.ShowUltimates)
            {
                try
                {
                    this._requestUltOnce = false;
                    if (this._ultimateTitles.Contains(this._target.title))
                    {
                        
                    }
                }
                catch (Exception) { }
            }*/
            
            // request character's parses data from fflogs
            if (_requestOnce || _config.forceRefresh)
            {
                this._requestOnce = false;
                _config.Save();
                var world = FflogRequestsHandler._worlds.Find(w => w.Name.ToString().Equals(this._target.homeWorld));
                var dc = FflogRequestsHandler._worldDcs.Find(d => d.Name.ToString().Equals(world.DataCenter.Value.Name));
                var region = FflogRequestsHandler._regionName[dc.Region];
                this._target.region = region;
                var temp = await _fflog.PerformRequest(this._target.ToTargetInfo());
                this.RaidingPerformance = _fflog.Summarize(temp, _target.ToTargetInfo());
            }
            else
            {
                try
                {
                    const string windowTitle = "percentiles";
                    ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize;

                    if (!this._config.ShowBackground) flags |= ImGuiWindowFlags.NoBackground;
                    // +73, -120 -> offsets to place percentiles right below the character preview window
                    ImGui.SetNextWindowPos(
                        new Vector2(
                            this._target.winPosX + this._target.offsetX + this._config.OffsetX,
                            this._target.winPosY + this._target.offsetY + this._config.OffsetY
                        )
                    );

                    ImGui.Begin(windowTitle, flags);
                    ImGui.BeginGroup();
                    if (this.RaidingPerformance.meta != null)
                    {
                        if (this.RaidingPerformance.meta.erroredProcessing)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, Grey);
                            ImGui.PushFont(UiBuilder.IconFont);
                            ImGui.Text(this.RaidingPerformance.meta.icon);
                            ImGui.PopFont();
                            ImGui.SameLine();
                            ImGui.Text(this.RaidingPerformance.meta.hoverText);
                            ImGui.PopStyleColor();
                            ImGui.EndGroup();
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                            }

                            if (ImGui.IsItemClicked())
                            {
                                Process.Start(
                                    $"https://www.fflogs.com/character/{this._target.region}/{this._target.homeWorld}/{this._target.firstName} {this._target.lastName}");
                            }

                            ImGui.End();
                            return;
                        }
                    }

                    var totalPercentiles = 0;
                    var cx = ImGui.GetCursorPosX();
                    var i = 0;
                    var spacing = 20;
                    
                    foreach (var fight in this.RaidingPerformance.fightsArray)
                    {
                        
                        var p1percentile = _config.ShowMedian
                            ? fight.medianPercentile
                            : fight.highestPercentile;
                        totalPercentiles += fight.highestPercentile;
                        ImGui.PushStyleColor(ImGuiCol.Text, GetColorFromPercentile(p1percentile));
                        ImGui.SetCursorPosX(cx + spacing * i);
                        ImGui.Text("" + (p1percentile switch
                        {
                            0 => "·",
                            //100 => "★",
                            _ => p1percentile
                        }));
                        ImGui.SameLine();
                        ImGui.PopStyleColor();
                        if (ImGui.IsItemHovered() && fight.kills != 0)
                        {
                            ImGui.BeginTooltip();
                            ImGui.Text($"{fight?.getShortName()} ({fight?.job}) (kills: {fight?.kills})");
                            ImGui.PushStyleColor(ImGuiCol.Text, Grey);
                            ImGui.Text(_config.ShowMedian ? "Median" : "Best %%");
                            ImGui.PopStyleColor();
                            ImGui.EndTooltip();
                        }
                        
                        if (fight.savage && fight.part2 != null)
                        {
                            var p2percentile = _config.ShowMedian
                                ? fight.part2.medianPercentile
                                : fight.part2.highestPercentile;
                            ImGui.SetCursorPosX(cx + (spacing * (float) (i + 0.725)));
                            ImGui.Text("/");
                            ImGui.SameLine();
                            ImGui.SetCursorPosX(cx + (spacing * (i + 1)));
                            ImGui.PushStyleColor(ImGuiCol.Text, GetColorFromPercentile(p2percentile));
                            ImGui.Text("" + (p2percentile switch
                            {
                                0 => "·",
                                //100 => "★",
                                _ => p2percentile
                            }));
                            ImGui.SameLine();
                            ImGui.PopStyleColor();
                            if (ImGui.IsItemHovered() && fight.kills != 0)
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text($"{fight.getShortName()} ({fight.part2.job}) (kills: {fight.part2.kills})");
                                ImGui.PushStyleColor(ImGuiCol.Text, Grey);
                                ImGui.Text(_config.ShowMedian ? "Median" : "Best %%");
                                ImGui.PopStyleColor();
                                ImGui.EndTooltip();
                            }
                        }
                        i++;
                    }
                    
                    
                    ImGui.NewLine();
                    ImGui.BeginGroup(); // items below parses group 
                    
                    var cury = ImGui.GetCursorPosY();
                    ImGui.SetCursorPosY(cury-25f);
                    
                    var showTierNameCond = _config.ShowTierName && _config.CurrentDisplayZoneID != Plugin.LATEST_RAID_ID;
                    if (showTierNameCond)
                    {
                        ImGui.NewLine();
                        ImGui.PushStyleColor(ImGuiCol.Text, Grey);
                        ImGui.Text(ConfigUI.zones[_config.CurrentDisplayZoneID].name);
                        ImGui.PopStyleColor();
                        ImGui.SameLine();
                        
                    }

                    var normalCond = (_config.ShowNormal || _config.ShowOnlyNormal)
                                     && (!this.RaidingPerformance.firstFight?.savage ?? false)
                                     && (totalPercentiles != 0 || _config.ShowOnlyNormal);
                    if (normalCond)
                    {
                        if (showTierNameCond)
                        {
                            var curx = ImGui.GetCursorPosX();
                            ImGui.SetCursorPosX(curx-5f);
                        }
                        else
                        {
                            ImGui.SetCursorPosY(cury-5f);
                        }
                        
                        ImGui.PushStyleColor(ImGuiCol.Text, Grey);
                        ImGui.Text("(Normal)");
                        ImGui.PopStyleColor();
                    }
                    ImGui.EndGroup();
                    if (ImGui.IsItemHovered() && normalCond)
                    {
                        ImGui.BeginTooltip();
                        ImGui.PushTextWrapPos(250f);
                        ImGui.TextWrapped(
                            $"The percentiles above were set in the normal version of {ConfigUI.zones[_config.CurrentDisplayZoneID].name}.");
                        ImGui.PopTextWrapPos();
                        ImGui.EndTooltip();
                    }
                    
                    ImGui.EndGroup();
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                    }

                    if (ImGui.IsItemClicked())
                    {
                        Process.Start(
                            $"https://www.fflogs.com/character/{this._target.region}/{this._target.homeWorld}/{this._target.firstName} {this._target.lastName}" +
                            (_config.CurrentDisplayZoneID != Plugin.LATEST_RAID_ID ? $"?zone={_config.CurrentDisplayZoneID}" : ""));
                    }

                    ImGui.End();
                }
                catch (Exception e)
                {
                    PluginLog.Log(e.Message); // there shouldn't be any problems now :) i think i fixed most of them...
                }
            }
        }

            

        public class InspectInfo
        {
            public string firstName;
            public string lastName;
            public string homeWorld;
            public string title;

            public float winHeight;
            public float winWidth;
            public int winPosX;
            public int winPosY;
            public float winScale;
            public float offsetX;
            public float offsetY;
            public string region;

            public TargetInfo ToTargetInfo()
            {
                return new TargetInfo(this.firstName, this.lastName, this.homeWorld);
            }
        }
    }
}
