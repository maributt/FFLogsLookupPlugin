using ImGuiNET;
using Dalamud.Plugin;
using FFXIVClientStructs.Component.GUI;
using System.Runtime.InteropServices;
using System;
using System.Numerics;
using System.Collections.Generic;
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

        private readonly Vector4 _yellow = new Vector4(0.898f, 0.8f, 0.501f, 1f);
        private readonly Vector4 _pink = new Vector4(0.886f, 0.408f, 0.659f, 1f);
        private readonly Vector4 _orange = new Vector4(1.0f, 0.5019f, 0.0f, 1.0f);
        private readonly Vector4 _purple = new Vector4(0.639f, 0.2078f, 0.933f, 1f);
        private readonly Vector4 _blue = new Vector4(0f, 0.439f, 1f, 1f);
        private readonly Vector4 _green = new Vector4(0.117f, 1f, 0f, 1f);
        private readonly Vector4 _grey = new Vector4(0.4f, 0.4f, 0.4f, 1f);

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

        /// <summary>
        /// Returns a color to dye the percentile text with (according to fflogs' website's colors) 
        /// </summary>
        /// <param name="percentile"></param>
        /// <returns></returns>
        private Vector4 GetColorFromPercentile(int percentile)
        {
            if (percentile < 25) return _grey;
            if (percentile < 50) return _green;
            if (percentile < 75) return _blue;
            if (percentile < 95) return _purple;
            if (percentile < 99) return _orange;
            return percentile < 100 ? _pink : _yellow;
        }

        public async void Draw()
        {
            // if the character inspect window is not visible we make sure fflogs data is requested when it next is
            if (!IsCharacterInspectVisible())
            {
                this._requestOnce = true;
                this.RaidingPerformance = new RaidingTierPerformance(0);
                return;
                /*if (!resetOnce) return;
                this.parseEntries = new List<int>{ 0, 0, 0, 0, 0 };
                errorFromSummarize = null;
                resetOnce = false;*/
                
            }
            
            // request character's parses data from fflogs
            if (_requestOnce)
            {
                this._requestOnce = false;
                var par = new FflogsRequestParams(this._target.ToTargetInfo(), false, false);
                var temp = 
                    await _fflog.PerformRequest(
                        new FflogsRequestParams(this._target.ToTargetInfo(), false, false)
                    );
                this.RaidingPerformance = _fflog.Summarize(temp, _target.ToTargetInfo());
                /*
                try
                {
                    var oogaValue = await fflog.PerformRequest(tiTarget);
                    var summarized = FflogRequestsHandler.Summarize(oogaValue, tiTarget);
                    //summarized.meta.erroredProcessing => leads to you pushing different stuff to the UI if there was an "error"
                    if ((summarized) != null || errorFromSummarize == null)
                    {
                        this.parseEntries = new List<int>() {};
                        foreach (var fight in summarized.fightsArray)
                        {
                            this.parseEntries.Add(fight.highestPercentile);
                            if (fight.part2 != null) this.parseEntries.Add(fight.part2.highestPercentile);
                        }
                    }
                } catch (Exception e)
                {
                    PluginLog.Log(e.Message);
                }*/
            }
            else
            {
                try
                {
                    const string windowTitle = "percentiles";
                    const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration
                                                   | ImGuiWindowFlags.NoInputs
                                                   | ImGuiWindowFlags.NoBackground
                                                   | ImGuiWindowFlags.AlwaysAutoResize;

                    //ImGui.SetNextWindowSize(new Vector2(180, 25));
                    // +73, -120 -> offsets to place percentiles below the character preview window
                    ImGui.SetNextWindowPos(
                        new Vector2(
                            this._target.winPosX +73,
                            this._target.winPosY + this._target.winHeight -120
                        )
                    );
                    
                    ImGui.Begin(windowTitle, flags);
                    if (this.RaidingPerformance.meta!=null)
                    {
                        if (this.RaidingPerformance.meta.erroredProcessing)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, this._grey);
                            ImGui.PushFont(UiBuilder.IconFont);
                            ImGui.Text(this.RaidingPerformance.meta.icon);
                            ImGui.PopFont(); ImGui.SameLine();
                            ImGui.Text(this.RaidingPerformance.meta.hoverText);
                            ImGui.PopStyleColor();
                        }
                    }
                    foreach (var fight in this.RaidingPerformance.fightsArray)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, GetColorFromPercentile(fight.highestPercentile));
                        ImGui.Text(""+( fight.highestPercentile switch
                        {
                            0 => "-",
                            100 => "★",
                            _ => fight.highestPercentile
                        })); ImGui.SameLine();
                        ImGui.PopStyleColor();
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.Text(fight.meta.hoverText ?? $"{fight?.getShortName()} ({fight?.job}) (kills: {fight?.kills})");
                            ImGui.EndTooltip();
                        }
                            
                        if (fight.part2 != null)
                        {
                            ImGui.Text("/"); ImGui.SameLine();
                            ImGui.PushStyleColor(ImGuiCol.Text, GetColorFromPercentile(fight.part2.highestPercentile));
                            ImGui.Text(""+( fight.part2.highestPercentile switch
                            {
                                0 => "-",
                                100 => "★",
                                _ => fight.part2.highestPercentile
                            })); ImGui.SameLine();
                            ImGui.PopStyleColor();
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text(fight.meta.hoverText ?? $"{fight.getShortName()} ({fight.part2.job}) (kills: {fight.part2.kills})");
                                ImGui.EndTooltip();
                            }
                        }

                    }
                    ImGui.End();
                    
                    /*else
                    {
                        PluginLog.Log(""+this.RaidingPerformance.fightsArray);
                        foreach (var fight in this.RaidingPerformance.fightsArray)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, GetColorFromPercentile(fight.highestPercentile));
                            ImGui.Text(""+( fight.highestPercentile switch
                            {
                                0 => "-",
                                100 => "★",
                                _ => fight.highestPercentile
                            }));
                            ImGui.PopStyleColor();
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text(fight.meta.hoverText ?? $"{fight.getShortName()} ({fight.job}) (kills: {fight.kills})");
                                ImGui.EndTooltip();
                            }
                            
                            if (fight.part2 != null)
                            {
                                ImGui.Text("/");
                                FormatFightEntryForImGui(fight.part2);
                            }
                            ImGui.Text(" ");
                            else
                            {
                                ImGui.Text(" ");
                            }

                        }
                    }*/

                    /*try
                    {
                        for (int i = 0; i < this.parseEntries.Count-1; i++)
                        {
                            var textPercentile = this.parseEntries[i] == 0 ? "-" : (this.parseEntries[i]==100? "★": this.parseEntries[i]+"") ;


                            ImGui.PushStyleColor(ImGuiCol.Text, GetColorFromPercentile(this.parseEntries[i]));
                            ImGui.Text(textPercentile);
                            if(ImGui.IsItemHovered())
                            {
                                ImGui.BeginPopup($"##fight{i}");
                                ImGui.Text($"{this.parseEntries[i]}");
                                ImGui.EndPopup();
                            }
                            ImGui.PopStyleColor();
                            ImGui.SameLine();
                            if (i == this.parseEntries.Count - 2)
                            {
                                ImGui.Text("/"); ImGui.SameLine();
                                ImGui.PushStyleColor(ImGuiCol.Text, GetColorFromPercentile(this.parseEntries[i + 1]));
                                ImGui.Text(this.parseEntries[i + 1] == 0 ? "-" : (this.parseEntries[i] == 100 ? "★" : this.parseEntries[i+1] + ""));
                                ImGui.PopStyleColor();
                            }
                        }
                    } catch (Exception e)
                    {
                        PluginLog.Log(e.Message);
                    }

                    if (errorFromSummarize != null)
                    {
                        ImGui.Text(errorFromSummarize);
                    }*/

                    
                }
                catch (Exception e)
                {
                    PluginLog.Log(e.Message);
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

            public TargetInfo ToTargetInfo()
            {
                return new TargetInfo(this.firstName, this.lastName, this.homeWorld);
            }
        }
    }
}
