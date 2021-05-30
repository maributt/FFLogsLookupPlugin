using ImGuiNET;
using Dalamud.Plugin;
using System.Runtime.InteropServices;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using HelperTypes;
using Lumina.Data;
using Lumina.Excel;
using Actor = Dalamud.Game.ClientState.Actors.Types.Actor;

namespace FFLogsLookup
{
    public class PluginUi
    {
        public bool IsVisible { get; set; }

        private readonly DalamudPluginInterface _interface;
        private readonly FflogRequestsHandler _fflog;
        private InspectInfo _target;
        private Dalamud.Game.ClientState.Actors.Types.Actor  _targetActor;
        private readonly Lumina.Excel.ExcelSheet<Lumina.Excel.GeneratedSheets.Title> _titles;
        private readonly Lumina.Excel.ExcelSheet<AddonParam>  _addonNames;

        private readonly List<string> _commonAddons = new List<string>()
        {
            "InventoryRetainerLarge", "InventoryRetainer", "InventoryExpansion", "InventoryLarge", "Inventory",
            "InventoryBuddy", "ArmouryBoard"
        };
        private readonly Configuration _config;
        private bool _requestOnce;
        private bool _requestUlts;
        private bool checkWeaponsOnce;
        private bool InspectWindowChanged;
        private bool SnapshotInspectedPlayer;
        public static readonly Vector4 Yellow = new Vector4(0.898f, 0.8f, 0.501f, 1f);
        public static readonly Vector4 Pink = new Vector4(0.886f, 0.408f, 0.659f, 1f);
        public static readonly Vector4 Orange = new Vector4(1.0f, 0.5019f, 0.0f, 1.0f);
        public static readonly Vector4 Purple = new Vector4(0.639f, 0.2078f, 0.933f, 1f);
        public static readonly Vector4 Blue = new Vector4(0f, 0.439f, 1f, 1f);
        public static readonly Vector4 Green = new Vector4(0.117f, 1f, 0f, 1f);
        public static readonly Vector4 Grey = new Vector4(0.4f, 0.4f, 0.4f, 1f);
        private RaidingTierPerformance RaidingPerformance { get; set; }
        public List<Fight> UltPerformance { get; set; }

        public PluginUi(DalamudPluginInterface pluginInterface, FflogRequestsHandler fflog, Configuration config)
        {
            this._interface = pluginInterface;
            this._fflog = fflog;
            this._config = config;
            this._target = new InspectInfo();
            this.checkWeaponsOnce = true;
            this.InspectWindowChanged = false;
            this.SnapshotInspectedPlayer = true;

            // ughhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh
            // load the lumina sheet for titles in whatever language the client is
            // ideally i would pass this: (Lumina.Data.Language)((int)Interface.ClientState.ClientLanguage)
            // as an argument but play the game in english please and thank you
            this._titles = _interface.Data.Excel.GetSheet<Lumina.Excel.GeneratedSheets.Title>();

            this._addonNames = _interface.Data.Excel.GetSheet<AddonParam>(); 
            this._requestOnce = true;
            this._requestUlts = false;
            this.RaidingPerformance = new RaidingTierPerformance(0);
            //this._requestUltOnce = true;
        }
        
        [Sheet("AddonParam")]
        private class AddonParam : Lumina.Excel.ExcelRow {
            public string XivString { get; private set; }
            public bool Boolean { get; private set; }

            public override void PopulateData(RowParser parser, Lumina.GameData gameData, Language language) {
                base.PopulateData(parser, gameData, language);
                XivString = parser.ReadColumn<string>(0);
                Boolean = parser.ReadColumn<bool>(1);
            }
        }

        public class WeaponDetails
        {
            public byte WeaponBase { get; }
            public byte WeaponVariant { get; }
            public short WeaponId { get; }
            public int FromEncounterId { get; set; }
            public int FromZoneId { get; set; }
            public bool ucob;
            public bool uwu;
            public bool tea;
            
            public static int mhWeaponIdOffset = 0xF08;
            public static int mhWeaponBaseOffset = 0xF0C;
            public static int mhWeaponVariantOffset = 0xF0A;
            

            public WeaponDetails(IntPtr Actor, bool IsOffHand=false)
            {
                this.WeaponId = Marshal.ReadInt16(Actor + mhWeaponIdOffset);
                this.WeaponVariant = Marshal.ReadByte(Actor + mhWeaponBaseOffset);
                this.WeaponBase = Marshal.ReadByte(Actor + mhWeaponVariantOffset);
                if (IsOffHand)
                {
                    this.WeaponId = Marshal.ReadInt16(Actor + 0xF70);
                    this.WeaponVariant = Marshal.ReadByte(Actor + 0xF74);
                    this.WeaponBase = Marshal.ReadByte(Actor + 0xF72);
                }
            }

            public WeaponDetails(short weaponId, byte weaponBase, byte weaponVariant)
            {
                this.WeaponId = weaponId;
                this.WeaponBase = weaponBase;
                this.WeaponVariant = weaponVariant;
                
            }

            public bool Equals(WeaponDetails obj)
            {
                var cond = (WeaponBase == obj.WeaponBase) && (WeaponVariant == obj.WeaponVariant) &&
                       (WeaponId == obj.WeaponId);
                //PluginLog.Log($"{this} == {obj} : {cond}");
                return cond;
            }

            public override string ToString()
            {
                return
                    $"model id: {WeaponId}, base: {WeaponBase}, variant: {WeaponVariant}"; //\nfrom encid: {FromEncounterId} - zoneid: {FromZoneId}";
            }
        }

        public static class Ultimate
        {
            public static class Weapons
            {
                #region The Unending Coil of Bahamut
                public static List<WeaponDetails> ucob = new List<WeaponDetails>()
                {
                    new WeaponDetails(201, 31, 2), // pld sword
                    new WeaponDetails(101, 32, 2), // pld shield
                    new WeaponDetails(401, 23, 2), // war
                    new WeaponDetails(1501, 11, 2), // drk
                    
                    new WeaponDetails(801, 23, 2), // whm
                    new WeaponDetails(1701, 4, 2), // sch
                    new WeaponDetails(2101, 6, 2), // ast
                    
                    new WeaponDetails(1701, 6, 2), // smn
                    new WeaponDetails(1001, 65, 1), // blm
                    new WeaponDetails(2301, 40, 2), // rdm
                    
                    new WeaponDetails(606, 1, 2), // brd
                    new WeaponDetails(2008, 1, 2), // CHONKY GUN
                    
                    new WeaponDetails(323, 20, 1), // mnk
                    new WeaponDetails(501, 21, 2), // drg
                    new WeaponDetails(1801, 3, 2), // nin
                    new WeaponDetails(2201, 28, 2) // sam
                };
                #endregion
                
                #region The Weapon's Refrain
                public static List<WeaponDetails> uwu = new List<WeaponDetails>()
                {
                    new WeaponDetails(201, 10, 8), // pld sword
                    new WeaponDetails(101, 11, 7), // pld shield
                    new WeaponDetails(401, 7, 8), // war
                    new WeaponDetails(1501, 37, 3), // drk
                    
                    new WeaponDetails(801, 6, 9), // whm
                    new WeaponDetails(1715, 1, 8), // sch
                    new WeaponDetails(2104, 1, 3), // ast
                    
                    new WeaponDetails(1705, 1, 8), // smn
                    new WeaponDetails(1001, 2, 9), // blm
                    new WeaponDetails(2301, 36, 2), // rdm
                    
                    new WeaponDetails(603, 1, 10), // brd
                    new WeaponDetails(2007, 1, 3), // not so chonky gun :(
                    
                    new WeaponDetails(323, 25, 1), // mnk
                    new WeaponDetails(504, 1, 9), // drg
                    new WeaponDetails(1801, 2, 8), // nin
                    new WeaponDetails(2207, 1, 2) // sam
                };
                #endregion
                
                #region The Epic of Alexander
                public static List<WeaponDetails> tea = new List<WeaponDetails>()
                {
                    new WeaponDetails(202, 121, 1), // pld sword
                    new WeaponDetails(101, 87, 1), // pld shield
                    new WeaponDetails(401, 96, 1), // war
                    new WeaponDetails(1501, 89, 1), // drk
                    new WeaponDetails(2501, 27, 1), // gnb my beloved :)

                    new WeaponDetails(808, 16, 1), // whm
                    new WeaponDetails(1715, 3, 1), // sch
                    new WeaponDetails(2106, 2, 1), // ast
                    
                    new WeaponDetails(1705, 4, 1), // smn
                    new WeaponDetails(1001, 85, 1), // blm
                    new WeaponDetails(2301, 62, 1), // rdm
                    
                    new WeaponDetails(601, 76, 1), // brd
                    new WeaponDetails(2012, 2, 1), // mch
                    new WeaponDetails(2601, 24, 1), // dnc
                    
                    new WeaponDetails(323, 36, 1), // beetles. .. .
                    new WeaponDetails(501, 88, 1), // drg
                    new WeaponDetails(1801, 89, 1), // nin
                    new WeaponDetails(2201, 49, 1), // sam
                };
                #endregion

                public static bool FindWeapon(ref WeaponDetails weapon)
                {
                    var found = false;
                    var weap = weapon;
                    
                    if (ucob.Count(w=>w.Equals(weap)) != 0)
                    {
                        weapon.FromZoneId = 30;
                        weapon.FromEncounterId = 1047;
                        weapon.ucob = true;
                        found = true;
                    }
                    else if (uwu.Count(w => w.Equals(weap)) != 0)
                    {
                        weapon.FromZoneId = 30;
                        weapon.FromEncounterId = 1048;
                        weapon.uwu = true;
                        found = true;
                    }
                    else if (tea.Count(w=>w.Equals(weap)) != 0)
                    {
                        weapon.FromZoneId = 32;
                        weapon.FromEncounterId = 1050;
                        weapon.tea = true;
                        found = true;
                    }
                    return found;
                }
            }

            public static class Titles
            {
                public static List<string> LegendTitles = new List<string>() {
                    "The Legend",
                    "The Ultimate Legend",
                    "The Perfect Legend",
                    "The Litgend:joy::joy::joy::joy::joy::joy::joy::joy::joy::joy::100:"
                };

                public static bool MatchTitle(string title, ref bool isUcob, ref bool isUwu, ref bool isTea)
                {
                    isUcob = false;
                    isUwu = false;
                    isTea = false;
                    
                    switch (title)
                    {
                        case "The Legend":
                            isUcob = true;
                            break;
                        case "The Ultimate Legend":
                            isUwu = true;
                            break;
                        case "The Perfect Legend":
                            isTea = true;
                            break;
                    }
                    
                    return isUcob || isUwu || isTea;
                }
                
            }
        }

        /// <summary>
        /// Returns the earliest available Actor object among the following: mouse over, target, focus target
        /// </summary>
        /// <returns>The actor object of the target if found, if not, returns null</returns>
        private Actor GetActorFromTarget()
        {
            var target = _interface.ClientState.Targets;
            return target.MouseOverTarget ?? target.CurrentTarget ?? target.FocusTarget;
        }
        
        /// <summary>
        /// Checks whether or not the addon InspectWindow is currently visible and populates this.Target accordingly
        /// </summary>
        /// <returns></returns>
        private unsafe bool IsCharacterInspectVisible()
        {
            var inspectWindow = (AtkUnitBase*)_interface.Framework.Gui.GetUiObjectByName("CharacterInspect", 1);
            var currentTarget = GetActorFromTarget();
            if (inspectWindow == null) return false;
            if (!inspectWindow->IsVisible)
            {
                if (_targetActor == null || InspectWindowChanged)
                {
                    SnapshotInspectedPlayer = true;
                    InspectWindowChanged = false;
                }
                _targetActor = null;
                _target = new InspectInfo();
                return false;
            }

            try
            {
                #region Resolve this._target fields from CharacterInspect Addon
                this._target.WinPosX = inspectWindow->X;
                this._target.WinPosY = inspectWindow->Y;
                this._target.WinHeight = inspectWindow->RootNode->Height;
                this._target.WinWidth = inspectWindow->RootNode->Width;
                this._target.WinScale = inspectWindow->Scale;

                this._target.OffsetX = 87f*this._target.WinScale;
                this._target.OffsetY = this._target.WinHeight +
                                       (float) (
                                           (-50 * Math.Pow(this._target.WinScale, 2f)) +
                                           595 * this._target.WinScale
                                           - 616);
                var inspectWindowNodeList = inspectWindow->ULDData.NodeList;
                this._target.WinFocused =
                    ((AtkComponentNode*) inspectWindowNodeList[1])->Component->ULDData.NodeList[3]->IsVisible;
                this._target.HomeWorld = Marshal.PtrToStringAnsi(new IntPtr(((AtkTextNode*)inspectWindowNodeList[59])->NodeText.StringPtr));
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
                        if (_target.Title != possibleTitle) InspectWindowChanged = true;
                        this._target.Title = possibleTitle;
                        name = Marshal.PtrToStringAnsi(new IntPtr(titleCandidate2->NodeText.StringPtr));
                    }
                    
                    else
                    {
                        name = possibleTitle;
                        var newTitle = Marshal.PtrToStringAnsi(new IntPtr(titleCandidate2->NodeText.StringPtr));
                        if (_target.Title != newTitle) InspectWindowChanged = true;
                        this._target.Title = newTitle;
                    }
                }
                else
                {
                    name = Marshal.PtrToStringAnsi(new IntPtr(titleCandidate2->NodeText.StringPtr));
                }
                if (name == null) return false; // basically impossible but my IDE wont stop screaming at me unless i put this here
                var aName = name.Split(' ');
                if (aName[0] != this._target.FirstName) InspectWindowChanged = true;
                this._target.FirstName = aName[0];
                this._target.LastName = aName[1];
                #endregion

                if (!_config.SnapshotActorExperimental || !SnapshotInspectedPlayer) return true;
                
                SnapshotInspectedPlayer = false;
                var actors = _interface.ClientState.Actors.Where(actor =>
                    actor.Name == $"{this._target.FirstName} {this._target.LastName}");
                var players = actors as Actor[] ?? actors.ToArray();

                if (players.Length == 0) return true;
                
                _targetActor = players.ToArray()[0];
                var mainhand = new WeaponDetails(_targetActor.Address);
                var offhand = new WeaponDetails(_targetActor.Address, true);
                    
                var foundMh = Ultimate.Weapons.FindWeapon(ref mainhand);
                var foundOh = Ultimate.Weapons.FindWeapon(ref offhand);
                var foundTi = Ultimate.Titles.MatchTitle(_target.Title, ref _target.ReqUcob, ref _target.ReqUwu, ref _target.ReqTea);
                if (foundTi || foundMh || foundOh)
                {
                    this._target.Mainhand = mainhand;
                    this._target.Offhand = offhand;
                    this._requestUlts = true;
                    PluginLog.Log("player has visibly cleared an ultimate (title/weapon)");
                }
                else
                {
                    PluginLog.Log("player hasn't cleared an ultimate (title/weapon)");
                }
                
                return true;
            } catch (Exception) 
                // ideally you wouldn't try/catch here but there is a specific scenario (right after first loading into a zone, before inspecting anyone) that can occur
                // where if you inspect someone the window will take longer than it usually would to truly display (a bit like the initial laoding time on the saddlebag but a lot less obvious to the player's eye)
            {
                return false;
            }


        }

        /// <summary>
        /// Returns a color to dye the percentile text with (according to fflogs' website's colors) 
        /// </summary>
        /// <param name="percentile"></param>
        /// <returns></returns>
        private static Vector4 GetColorFromPercentile(int percentile)
        {
            if (percentile < 25) return Grey;
            if (percentile < 50) return Green;
            if (percentile < 75) return Blue;
            if (percentile < 95) return Purple;
            if (percentile < 99) return Orange;
            return percentile < 100 ? Pink : Yellow;
        }

        private bool ItemTooltipHidesParses()
        {
            var tooltip = _interface.Framework.Gui.GetAddonByName("Tooltip", 1);
            if (tooltip?.Visible ?? true) return false;

            var itemdetail = _interface.Framework.Gui.GetAddonByName("ItemDetail", 1);
            // if the addon and the imgui window overlap...
            if (itemdetail.X < this._target.WinPosX + this._target.OffsetX + this._config.OffsetX
                && itemdetail.X + itemdetail.Width > this._target.WinPosX + this._target.OffsetX + this._config.OffsetX
                && itemdetail.Y < this._target.WinPosY + this._target.OffsetY + this._config.OffsetY
                && itemdetail.Y + itemdetail.Height >
                this._target.WinPosY + this._target.OffsetY + this._config.OffsetY)
            {
                return itemdetail.Visible;
            }

            return false;
        }

        /// <summary>
        /// This method needs a bit more testing, though the method itself should be fine if certain addons are passed it seems to crash the game
        /// for example the commendation menu when brought up alongside the CharacterInspect window will produce said result
        /// </summary>
        /// <param name="AddonName">The name of the addon to check the position and focus of</param>
        /// <returns>Whether the given addon overlaps the character inspect window or not</returns>
        private unsafe bool AddonHidesParses(string AddonName)
        {
            if (AddonName == null || AddonName == "CharacterInspect") return false;
            var addon = _interface.Framework.Gui.GetUiObjectByName(AddonName, 1);
            if (addon == IntPtr.Zero) return false;
            try
            {
                
                //if the addon is not focused
                if (!((AtkComponentNode*) ((AtkUnitBase*)addon)->ULDData.NodeList[1])->Component->ULDData.NodeList[3]->IsVisible) 
                    return false;
                var a = new Dalamud.Game.Internal.Gui.Addon.Addon(addon,
                    Marshal.PtrToStructure<Dalamud.Game.Internal.Gui.Structs.Addon>(addon));
                var imguiwinStartX = this._target.WinPosX + this._target.OffsetX + this._config.OffsetX;
                var imguiwinStartY = this._target.WinPosY + this._target.OffsetY + this._config.OffsetY;
                if ( 
                    ((a.X < imguiwinStartX && 
                    a.X + a.Width > imguiwinStartX + 10) ||
                    (a.X > imguiwinStartX && 
                     a.X < imguiwinStartX+118*_target.WinScale)) 
                    
                    && 
                    
                    ((a.Y < imguiwinStartY && 
                    a.Y + a.Height > imguiwinStartY + 25) ||
                    (a.Y > imguiwinStartY &&
                     a.Y > imguiwinStartY+37*_target.WinScale))
                    )
                    return a.Visible;
                return false;
            } catch (Exception e)
            {
                PluginLog.Log(e.Message);
                return false;
            }

        }

        private static string GetRegionFromWorld(string worldName)
        {
            var world = FflogRequestsHandler._worlds.Find(w => w.Name.ToString().Equals(worldName));
            var dc = FflogRequestsHandler._worldDcs.Find(d => d.Name.ToString().Equals(world.DataCenter.Value.Name));
            return FflogRequestsHandler._regionName[dc.Region];
        }
        
        /// <summary>
        /// Draws the reminder ImGui window under the character inspect window of an inspected player until the user has completed the basic setup process
        /// </summary>
        private void DrawReminder()
        {
            ImGui.SetNextWindowPos(
                new Vector2(
                    this._target.WinPosX +10,
                    this._target.WinPosY + this._target.WinHeight -65
                )
            );
            ImGui.Begin("reminder",ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0f, 0f, 1f));
            ImGui.Text("You must complete the initial configuration\nbefore you can start looking at other players' logs!");
            ImGui.PopStyleColor();
            ImGui.End();
        }

        private void IconText(string text)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text(text);
            ImGui.PopFont();
        }

        /// <summary>
        /// Displays a hover text made from a given Fight object if the given fight has been cleared (>0 kills)
        /// </summary>
        /// <param name="fight"></param>
        private void HoverParseEntry(Fight fight)
        {
            if (!ImGui.IsItemHovered() || fight.kills == 0) return;
            ImGui.BeginTooltip();
            ImGui.Text($"{fight?.getShortName()} ({fight?.job}) (kills: {fight?.kills})");
            ImGui.PushStyleColor(ImGuiCol.Text, Grey);
            ImGui.Text(_config.ShowMedian ? "Median" : "Best %%");
            ImGui.PopStyleColor();
            ImGui.EndTooltip();
        }

        /// <summary>
        /// Draws the percentile of the given fight as text color-coded to match FFLogs' color standards
        /// </summary>
        /// <param name="fight"></param>
        private void DrawEntry(Fight fight)
        {
            var percentile = _config.ShowMedian
                ? fight.medianPercentile
                : fight.highestPercentile;
            ImGui.TextColored(GetColorFromPercentile(percentile), ""+percentile switch
            {
                -1=>"•",
                0 => "·",
                100 => "★",
                _ => percentile
            });
            ImGui.SameLine();
        }

        /// <summary>
        /// Display the tier name of the displayed parses if the tier isn't the latest one and if the user has the option enabled in the config
        /// </summary>
        private bool ShowTierName()
        {
            var showTierNameCond = _config.ShowTierName && _config.CurrentDisplayZoneID != Plugin.LATEST_RAID_ID;
            if (!showTierNameCond) return false;
            
            ImGui.NewLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Grey);
            ImGui.Text(ConfigUI.zones[_config.CurrentDisplayZoneID].name);
            ImGui.PopStyleColor();
            ImGui.SameLine();
            return true;
        }

        private bool ShowNormalLabel(bool TierNameDrawn, int PercentileSum)
        {
            var normalCond = (_config.ShowNormal || _config.ShowOnlyNormal)
                             && (!this.RaidingPerformance.firstFight?.savage ?? false)
                             && (PercentileSum != 0 || _config.ShowOnlyNormal)
                             && !this.RaidingPerformance.firstFight.extreme;
            if (!normalCond) return false;
            if (TierNameDrawn) ImGui.SetCursorPosX(ImGui.GetCursorPosX()-5f);
            else ImGui.SetCursorPosY(ImGui.GetCursorPosY()+20f);
            
            ImGui.PushStyleColor(ImGuiCol.Text, Grey);
            ImGui.Text("(Normal)");
            ImGui.PopStyleColor();
            return true;
        }

        /// <summary>
        /// Displays information when hovering over a normal difficulty drawn entry 
        /// </summary>
        /// <param name="Show"></param>
        private void HoverNormalMode(bool Show)
        {
            if (!ImGui.IsItemHovered() || !Show) return;
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(250f);
            ImGui.TextWrapped(
                $"The percentiles above were set in the normal version of {ConfigUI.zones[_config.CurrentDisplayZoneID].name}.");
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }

        /// <summary>
        /// Handles the drawing process of messages displayed if the character's logs are not found or hidden
        /// </summary>
        private void DrawSpecialCase()
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Grey); 
            IconText(this.RaidingPerformance.meta.icon); ImGui.SameLine();
            ImGui.Text(this.RaidingPerformance.meta.hoverText);
            ImGui.PopStyleColor();
            ImGui.EndGroup();
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (this.RaidingPerformance.meta.hoverText == FflogRequestsHandler.LOGS_NOT_FOUND)
                {
                    ImGui.BeginTooltip();
                    ImGui.PushStyleColor(ImGuiCol.Text, Grey);
                    ImGui.Text(!_interface.ClientState.KeyState[0x10]
                        ? "Hold Left Shift and click to open a new fflogs search for the character.\n(can be useful to know if character has no logs\n instead of just having transferred to a new world)"
                        : $"Click to open a new search window for \"{this._target.FirstName} {this._target.LastName}\".");
                    ImGui.PopStyleColor();
                    ImGui.EndTooltip();
                }
            }
            if (ImGui.IsItemClicked()) Process.Start(
                (_interface.ClientState.KeyState[0x10] && this.RaidingPerformance.meta.hoverText == FflogRequestsHandler.LOGS_NOT_FOUND)
                    ? $"https://www.fflogs.com/search/?term={System.Net.WebUtility.UrlEncode(this._target.FirstName+" "+this._target.LastName)}"
                    : $"https://www.fflogs.com/character/{this._target.Region}/{this._target.HomeWorld}/{this._target.FirstName} {this._target.LastName}"
            );

            ImGui.End();
        }
        
        private void DrawPercentiles()
        {
            var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize;
            if (!this._config.ShowBackground) flags |= ImGuiWindowFlags.NoBackground;
            ImGui.SetNextWindowPos(
                new Vector2(
                    this._target.WinPosX + this._target.OffsetX + this._config.OffsetX,
                    this._target.WinPosY + this._target.OffsetY + this._config.OffsetY
                )
            );
            ImGui.Begin("percentiles", flags);
            ImGui.BeginGroup();
            
                // Logs not found / Hidden Logs
                if (this.RaidingPerformance.meta != null)
                {
                    if (this.RaidingPerformance.meta.erroredProcessing)
                    {
                        DrawSpecialCase(); return;
                    }
                }
                
                var totalPercentiles = 0;
                var cx = ImGui.GetCursorPosX();
                float spacing = 3 + ImGui.GetFontSize();
                for (var i = 0; i < this.RaidingPerformance.fightsArray.Length; i++)
                {
                    var fight = this.RaidingPerformance.fightsArray[i];
                    ImGui.SetCursorPosX(cx + spacing * i);
                    DrawEntry(fight);
                    HoverParseEntry(fight);
                    if (fight.savage && fight.part2 != null)
                    {
                        ImGui.SetCursorPosX(cx + (spacing * (float) (i + 0.725)));
                        ImGui.Text("/"); ImGui.SameLine();
                        
                        ImGui.SetCursorPosX(cx + (spacing * (i + 1)));
                        DrawEntry(fight.part2);
                        HoverParseEntry(fight.part2);
                    }
                    totalPercentiles += fight.highestPercentile;
                }
                

                ImGui.NewLine(); ImGui.BeginGroup(); // items below parses group 
                
                    var cury = ImGui.GetCursorPosY();
                    ImGui.SetCursorPosY(cury-25f);
                    
                    var TierNameShown = ShowTierName();
                    var NormalLabelShown = ShowNormalLabel(TierNameShown, totalPercentiles);
                
                ImGui.EndGroup();
                HoverNormalMode(NormalLabelShown);
            ImGui.EndGroup();
            if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsItemClicked()) Process.Start(
                    $"https://www.fflogs.com/character/{this._target.Region}/{this._target.HomeWorld}/{this._target.FirstName} {this._target.LastName}" +
                    (_config.CurrentDisplayZoneID != Plugin.LATEST_RAID_ID ? $"?zone={_config.CurrentDisplayZoneID}" : ""));
            ImGui.End();
        }
        
        public async void Draw()
        {
            /* skip drawing UI if the character inspect addon is not visible
             get ready for the next inspect */
            if (!IsCharacterInspectVisible())
            {
                this._requestOnce = true;
                this.RaidingPerformance = new RaidingTierPerformance(-1);
                this.UltPerformance = null;
                return;
            }
            // draw reminder to do the initial setup
            if (_config.initialConfig)
            {
                DrawReminder();
                return;
            }
            // skip drawing UI if an itemtooltip would hide it
            var cond = (_config.DetectOverlaps) && (!_target.WinFocused) && (_commonAddons.Any(AddonHidesParses));
            if (ItemTooltipHidesParses() || cond) return;

            // call the FFLogs API once per character inspect
            if (_requestOnce || _config.forceRefresh)
            {
                this._requestOnce = false;
                this._requestUlts = false; 
                _config.Save();
                
                // resolve the DC region field for the inspected character's InspectInfo object
                this._target.Region = GetRegionFromWorld(this._target.HomeWorld);
                
                // Request usual savage / extreme logs for whichever tier is selected in the config
                var temp = await _fflog.PerformRequest(this._target.ToTargetInfo());
                if (temp == null)
                {
                    this.RaidingPerformance = new RaidingTierPerformance(-1)
                    {
                        meta = new Meta()
                        {
                            erroredProcessing = true,
                            hoverText = "FFLogs offline.",
                            icon = FontAwesomeIcon.Times.ToIconString(),
                            longHoverText = "FFLogs website is down for a short maintenance."
                        }
                    };
                    return;
                }
                this.RaidingPerformance = _fflog.Summarize(temp);
                return;
            }
            
            try
            {
                DrawPercentiles();
            }
            catch (Exception e)
            {
                PluginLog.Log(e.Message); // there shouldn't be any problems now :) i think i fixed most of them...
            }
        }
        
        public class InspectInfo
        {
            public string FirstName;
            public string LastName;
            public string HomeWorld;
            public string Region;
            public string Title;
            
            public bool ReqUcob;
            public bool ReqUwu;
            public bool ReqTea;
            
            public WeaponDetails Mainhand;
            public WeaponDetails Offhand;

            public float WinHeight = 0f;
            public float WinWidth = 0f;
            public int WinPosX = 0;
            public int WinPosY = 0;
            public float WinScale = 1f;
            public float OffsetX = 87f;
            public float OffsetY = -120f;
            public bool WinFocused;
            
            public TargetInfo ToTargetInfo()
            {
                return new TargetInfo(this.FirstName, this.LastName, this.HomeWorld);
            }
        }
    }
}
