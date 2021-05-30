using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Plugin;
using ImGuiNET;
using Lumina.Data.Parsing;

namespace FFLogsLookup
{
    public class ConfigUI
    {
        private Configuration config;
        public bool IsVisible { get; set; }
        private int OffsetX;
        private int OffsetY;
        private bool ShowBackground;
        private bool ShowNormal;
        private bool ShowOnlyNormal;
        private bool ShowUltimates;
        private bool ShowTierName;

        private int CurrentDisplayTier;        
        private string client_id;
        private string client_secret;
        public Vector4 grey = new Vector4(1f, 1f, 1f, 0.6f);
        public Vector4 linkcol = new Vector4(0f, 0.561f, 0.957f, 1f);
        public Vector4 hlCol = new Vector4(0.274f, 0.772f, 1f, 1f);
        public Vector4 commentCol = new Vector4(1f, 1f, 1f, 0.4f);
        public Vector4 cidCol = new Vector4(0f, 1f, 0f, 1f);
        public Vector4 csecCol = new Vector4(1f, 0f, 0f, 1f);
        private readonly FflogRequestsHandler fflog;
        private bool testReqSucceeded = false;
        private bool testReqSent = false;
        private bool testReqPending = false;
        private int testReqCode;
        private string workingId;
        private string workingSecret;
        private bool InitialDrawReq;
        private bool PercentileShown;
        private bool SnapshotActor;
        private bool DetectOverlaps;
        
        //offsets for draw
        private float labeloffset = -10f;
        private float rightOffset = 240f;
        private float offset = 200f;
        private float rightBoundOffset = 120f;
        private float credsOffset = 100f;
        private DalamudPluginInterface Interface;
        
        public static  Dictionary<int, ZoneDesc> zones = new Dictionary<int, ZoneDesc>() {
            { 38, new ZoneDesc("Eden's Promise")},
            { 33, new ZoneDesc("Eden's Verse")},
            { 29, new ZoneDesc("Eden's Gate")},
            { 25, new ZoneDesc("Omega: Alphascape")}, // below this line don't work yet, something to do with Summarize() (generalize it more, more checks, etc)
            { 21, new ZoneDesc("Omega: Sigmascape")},
            { 17, new ZoneDesc("Omega: Deltascape")},
            { 37, new ZoneDesc("Trials III (Extreme)", "Emerald Weapon (I&II), Diamond Weapon")},
            { 34, new ZoneDesc("Trials II (Extreme)", "Ruby Weapon (I&II), Varis Yae Galvus, Warrior of Light")},
            { 28, new ZoneDesc("Trials I (Extreme)", "Titania, Innocence, Hades")}
        };

        public class ZoneDesc
        {
            public string name;
            public string desc;
            public ZoneDesc(string name, string desc)
            {
                this.name = name;
                this.desc = desc;
            }

            public ZoneDesc(string name)
            {
                this.name = name;
                this.desc = null;
            }
        }
        public static string[] zoneNames = zones.Values.Select(zDesc => zDesc.name).ToArray();
        public static int[] zoneIDs = zones.Keys.ToArray();

        public static int[] SavageZoneIDs = new[]
        {
            38, 
            33, 
            29,
        };
        public static int[] SavageZoneNoDiffFilterIDs = new[]
        {
            25, // Alphascape (Savage)
            21, // Sigmascape (Savage)
            17, // Deltascape (Savage)
            13, // Creator (Savage)
            10, // Midas (Savage)
            7,  // Gordias (Savage)
        };

        public static int[] ExTrialIDs =
        {
            37, 34, 28
        };
        public ConfigUI(DalamudPluginInterface pluginInterface, Configuration config, FflogRequestsHandler fflog)
        {
            this.config = config;
            this.OffsetX = config.OffsetX;
            this.OffsetY = config.OffsetY;
            this.ShowOnlyNormal = config.ShowOnlyNormal;
            this.ShowNormal = config.ShowNormal;
            this.ShowUltimates = config.ShowUltimates;
            this.ShowBackground = config.ShowBackground;
            this.client_id = config.client_id ?? "";
            this.client_secret = config.client_secret ?? "";
            this.PercentileShown = config.ShowMedian;
            this.ShowTierName = config.ShowTierName;
            this.CurrentDisplayTier = config.TierIndex;
            this.SnapshotActor = config.SnapshotActorExperimental;
            this.fflog = fflog;
            this.Interface = pluginInterface;
            this.DetectOverlaps = config.DetectOverlaps;
        }

        


        #region Draw Initial (Tutorial) interface
        public void DrawInitialSetup()
        {
            if (!config.initialConfig)
            {
                this.Interface.UiBuilder.OnBuildUi -= DrawInitialSetup;
                InitialDrawReq = false;
                return;
            }

            if (Interface.ClientState.LocalPlayer == null)
            {
                return;
            }

            InitialDrawReq = true;
            IsVisible = true;
            
            // this is such a mess but oh well
            ImGui.Begin("initial setup##idfkkkhelpme", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize);
            ImGui.NewLine();
            ImGui.Text($"Thank you for installing FFLogs Lookup!");
            ImGui.Text("Before you can start using the plugin as intended you will have to go through a very simple few steps listed below!");
            ImGui.NewLine();
            ImGui.Text("   1. Go to the following url:"); ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, linkcol); 
            ImGui.Text("https://www.fflogs.com/api/clients/"); ImGui.SameLine();
                if (ImGui.IsItemHovered())
                {
                    linkcol = new Vector4(0f, 0.851f, 1f, 1f);
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }
                else
                {
                    linkcol = hlCol;
                }
                if (ImGui.IsItemClicked())
                {
                    Process.Start("https://www.fflogs.com/api/clients/");
                }
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Text, commentCol);
            ImGui.Text("(click to open)");
            ImGui.PopStyleColor();
            ImGui.NewLine();
                
            var cx = ImGui.GetCursorPosX();
            ImGui.Text("   2. Click on"); ImGui.SameLine(); ImGui.SetCursorPosX(cx+70);
            ImGui.PushStyleColor(ImGuiCol.Text, hlCol);
            ImGui.Text("+Create Client"); ImGui.SameLine();
            ImGui.PopStyleColor();
            ImGui.SetCursorPosX(cx);
            ImGui.NewLine();
            ImGui.NewLine();
            
            ImGui.Text("   3. Name your application");
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, hlCol);
            ImGui.Text("fflogslookup-dalamud-plugin");
            ImGui.PopStyleColor(); ImGui.SameLine();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }
                if (ImGui.IsItemClicked())
                {
                    ImGui.SetClipboardText("fflogslookup-dalamud-plugin");
                }
            ImGui.PushStyleColor(ImGuiCol.Text, commentCol);
            ImGui.Text("(click to copy)");
            ImGui.PopStyleColor();
            ImGui.PushStyleColor(ImGuiCol.Text, commentCol);
            ImGui.Text("(the name could be anything you'd like but for simplicity's sake you can just use this one)");
            ImGui.PopStyleColor();
            
            ImGui.NewLine();
            ImGui.Text("   4. Put"); ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, hlCol);
            ImGui.Text("http://localhost");
            ImGui.PopStyleColor(); ImGui.SameLine();
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }
                if (ImGui.IsItemClicked())
                {
                    ImGui.SetClipboardText("http://localhost");
                }
            ImGui.PushStyleColor(ImGuiCol.Text, commentCol);
            ImGui.Text("(click to copy)"); ImGui.SameLine();
            ImGui.PopStyleColor();
            ImGui.Text("in the field below the name!");
            ImGui.PushStyleColor(ImGuiCol.Text, commentCol);
            ImGui.Text("(the actual redirect url you put here does not matter but again, you can just use the one provided here)");
            ImGui.PopStyleColor();

            ImGui.NewLine();
            ImGui.Text("   5. Click the"); ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, hlCol);
            ImGui.Text("Create"); ImGui.SameLine();
            ImGui.PopStyleColor(); 
            ImGui.Text("button and copy the"); ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, cidCol);
            ImGui.Text("client ID"); ImGui.SameLine();
            ImGui.PopStyleColor(); 
            ImGui.Text("and the"); ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, csecCol);
            ImGui.Text("client secret"); ImGui.SameLine();
            ImGui.PopStyleColor(); 
            ImGui.Text("in the config window!");
            ImGui.NewLine();
            
            ImGui.Text("Then just click the"); ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, hlCol);
            ImGui.Text("Test connection to API"); ImGui.SameLine();
            ImGui.PopStyleColor();
            ImGui.Text("button and you're good to go!");
            ImGui.NewLine();
            ImGui.End();
        }
        #endregion

        public void Draw()
        {
            if (!IsVisible&&!this.InitialDrawReq)
                return;


            try
            {
                ImGui.Begin("config##configwindow", ImGuiWindowFlags.NoDecoration
                                      | ImGuiWindowFlags.AlwaysAutoResize);

                var cx = ImGui.GetCursorPosX() + 20;
                var cy = ImGui.GetCursorPosY() + 20;

                ImGui.SetCursorPosY(cy+3);
                ImGui.SetCursorPosX(cx+rightOffset+rightBoundOffset);
                ImGui.SetCursorPosX(cx+labeloffset);
                var text = "Detect overlapping windows? ";
                ImGui.SetCursorPosX(172);
                ImGui.Text(text); ImGui.SameLine();
                if (ImGui.Checkbox("##overlapsDetectionToggle", ref DetectOverlaps))
                {
                    config.DetectOverlaps = DetectOverlaps;
                } ImGui.SameLine();
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.PushTextWrapPos(350f);
                    ImGui.PushStyleColor(ImGuiCol.Text, grey);
                    ImGui.TextWrapped("Hide the parses display when other windows are overlapping the Character Inspect one, this may or may not impact performance so I'm leaving a toggle for it here for the time being until I know for sure whether it does or not.\nAs is, it should be stable enough to be used and from my limited testing, you probably won't see a performance drop.");
                    ImGui.PopStyleColor();
                    ImGui.EndTooltip();
                }
                ImGui.SetCursorPosY(cy);
                ImGui.SetCursorPosX(cx+rightOffset+rightBoundOffset);
                ImGui.SetCursorPosX(cx+labeloffset);

                #region ImGui Display Win Config
                    ImGui.Text("Parse Settings");
                    ImGui.NewLine();
                    ImGui.SetCursorPosX(cx);
                    cy = ImGui.GetCursorPosY();
                    
                    // window bg
                    ImGui.Text("Show window background");
                    ImGui.SameLine(); ImGui.SetCursorPosX(cx+offset);
                    if (ImGui.Checkbox("##background", ref ShowBackground))
                    {
                        config.ShowBackground = ShowBackground;
                        config.Save();
                    }
                    ImGui.SetCursorPosX(cx);

                    // normal parses
                    ImGui.Text("Show normal mode parses");
                    ImGui.SameLine(); ImGui.SetCursorPosX(cx+offset);
                    if (ImGui.Checkbox("##nm", ref ShowNormal))
                    {
                        config.ShowNormal = ShowNormal;
                        config.Save(true);
                    }
                    ImGui.SetCursorPosX(cx);
                    
                    // select highest/avg
                    ImGui.Text("Show highest or median percentile"); ImGui.SameLine();
                    ImGui.SetCursorPosX(cx+offset);
                    if (ImGui.Checkbox("(  showing##bestPercentile", ref PercentileShown))
                    {
                        config.ShowMedian = PercentileShown;
                        config.Save();
                    }
                    
                    ImGui.SameLine(); 
                    ImGui.PushStyleColor(ImGuiCol.Text, PercentileShown ? PluginUi.Purple : PluginUi.Yellow);
                    ImGui.Text(PercentileShown ? "median" : "highest");
                    ImGui.PopStyleColor(); ImGui.SameLine(); 
                    ImGui.Text(")");
                    ImGui.SetCursorPosX(cx);

                    /* stuff that i havent implemented yet
                    // ult parses
                    ImGui.Text("Show ultimate parses");
                    ImGui.SameLine(); ImGui.SetCursorPosX(cx+offset);
                    if (ImGui.Checkbox("##ult", ref ShowUltimates))
                    {
                        config.ShowUltimates = ShowUltimates;
                    }
                    ImGui.SetCursorPosX(cx);

                    // only normal parses
                    ImGui.Text("Show only normal mode parses");
                    ImGui.SameLine(); ImGui.SetCursorPosX(cx+offset);
                    if (ImGui.Checkbox("##youareinsane", ref ShowOnlyNormal))
                    {
                        config.ShowOnlyNormal = ShowOnlyNormal;
                    }
                    ImGui.SetCursorPosX(cx);
                    */

                    // offsets
                    var cy_b = ImGui.GetCursorPosY();
                    ImGui.SetCursorPosY(cy);
                    
                    //x
                    
                    ImGui.SetCursorPosX(cx+rightOffset);
                    ImGui.Text("X Offset ");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50f);
                    if (ImGui.DragInt("##xoffset", ref OffsetX, 1))
                    {
                        config.OffsetX = OffsetX;
                        config.Save();
                    }
                    
                    //y
                    ImGui.SetCursorPosX(cx+rightOffset);
                    ImGui.Text("Y Offset ");
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(50f);
                    if (ImGui.DragInt("##yoffset", ref OffsetY, 1))
                    {
                        config.OffsetY = OffsetY;
                        config.Save();
                    }
                    ImGui.SetCursorPosY(cy_b);
                    ImGui.NewLine();
                    ImGui.Separator();
                    ImGui.NewLine();
                    
                    // tier selector
                    //ImGui.Combo("Select tier to display##tierSelector", "test");*
                    ImGui.SetCursorPosX(cx);
                    ImGui.Text("Select tier to display"); ImGui.SameLine();
                    ImGui.SetNextItemWidth(200f);
                    if (ImGui.Combo("##tierDisplayed", ref CurrentDisplayTier, zoneNames, zoneNames.Length))
                    {
                        config.CurrentDisplayZoneID = zoneIDs[CurrentDisplayTier];
                        config.TierIndex = CurrentDisplayTier;
                        config.Save(true);
                    };
                    if (ImGui.IsItemHovered())
                    {
                        if (zones[config.CurrentDisplayZoneID].desc != null)
                        {
                            ImGui.BeginTooltip();
                            ImGui.Text(zones[config.CurrentDisplayZoneID].desc);
                            ImGui.EndTooltip();
                        }
                    }
                    ImGui.SetCursorPosX(cx);
                    ImGui.Text("Display tier name under percentiles?"); ImGui.SameLine();
                    if (ImGui.Checkbox("##showTierName", ref ShowTierName))
                    {
                        config.ShowTierName = ShowTierName;
                        config.Save();
                    }
                    #endregion
                
                
                //separator
                ImGui.NewLine();
                ImGui.Separator();
                ImGui.NewLine();

                //client id and secret inputs
                ImGui.SetCursorPosX(cx+labeloffset);
                ImGui.Text("FFLogs API Settings");
                ImGui.NewLine();
                ImGui.SetCursorPosX(cx);
                ImGui.SetCursorPosX(cx);
                
                if (config.initialConfig)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, cidCol);
                    ImGui.Text("Client ID ");
                    ImGui.PopStyleColor();
                }
                else
                {
                    ImGui.Text("Client ID ");
                }
                ImGui.SameLine();
                ImGui.SetCursorPosX(cx+credsOffset);
                ImGui.SetNextItemWidth(245f);
                if (ImGui.InputText(" ##client_id", ref this.client_id, 36))
                {
                    this.testReqSent = false;
                    this.testReqPending = false;
                    this.testReqSucceeded = false;
                }
                ImGui.SetCursorPosX(cx);

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.PushTextWrapPos(450f);
                    ImGui.TextWrapped(
                        $"Input the client ID as provided by the FFLogs API upon client creation here!\n" +
                        $"If you lose this information (or overwrite it by mistake) you can find it again on the clients management page in your FFLogs account");
                    ImGui.PopTextWrapPos();
                    ImGui.EndTooltip();
                }

                if (config.initialConfig)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, csecCol);
                    ImGui.Text("Client Secret ");
                    ImGui.PopStyleColor();
                }
                else
                {
                    ImGui.Text("Client Secret ");
                }
                ImGui.SameLine();
                ImGui.SetCursorPosX(cx+credsOffset);
                ImGui.SetNextItemWidth(245f);
                if (ImGui.InputText(" ##client_secret", ref this.client_secret, 40, (!config.initialConfig?ImGuiInputTextFlags.Password:ImGuiInputTextFlags.None )))
                {
                    this.testReqSent = false;
                    this.testReqPending = false;
                    this.testReqSucceeded = false;
                }
                ImGui.SetCursorPosX(cx);

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.PushTextWrapPos(400f);
                    ImGui.TextWrapped($"Input the client secret as provided by the FFLogs API upon client creation here!");
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0f, 0f, 1f));
                    ImGui.TextWrapped("Warning: If you lose this, you will have to delete the client you made and create a new one" +
                        $" (as mentioned on the fflogs client creation page, so make sure to not overwrite it here by mistake)");
                    ImGui.PopStyleColor();
                    ImGui.PopTextWrapPos();
                    ImGui.EndTooltip();
                }

                ImGui.SetCursorPosX(cx+credsOffset);
                if (config.initialConfig) ImGui.PushStyleColor(ImGuiCol.Text, hlCol);
                if (ImGui.Button("Test connection to API"))
                {
                    this.testReqSent = false;
                    this.testReqPending = true;
                    fflog.TestRequest(client_id, client_secret).ContinueWith(res =>
                    {
                        this.testReqSent = true;
                        this.testReqPending = false;
                        this.testReqSucceeded = res.Result == 1;
                        this.testReqCode = res.Result;
                        if (testReqSucceeded)
                        {
                            this.workingId = this.client_id;
                            this.workingSecret = this.client_secret;
                            config.initialConfig = false; // dont care im putting this here
                            config.client_id = this.workingId;
                            config.client_secret = this.workingSecret;
                            config.Save();
                        }
                        else
                        {
                            if (this.workingId == null || this.workingSecret == null) return;
                            this.client_id = this.workingId;
                            this.client_secret = this.workingSecret;
                        }
                    });
                }
                if (config.initialConfig) ImGui.PopStyleColor();
                ImGui.SetCursorPosX(cx);

                ImGui.SameLine();
                if (this.testReqSent)
                {
                    var msgColor = testReqCode switch
                    {
                        2 => new Vector4(1f, 0.83f, 0.2f, 1f),
                        1 => new Vector4(0f, 1f, 0f, 1f),
                        0 => new Vector4(1f, 0f, 0f, 1f)
                    };
                    ImGui.PushStyleColor(ImGuiCol.Text, msgColor);
                    ImGui.Text(testReqCode switch {
                        2 => "FFLogs is down...",
                        1 => " Success!",
                        0 => " Incorrect credentials."
                    });
                    ImGui.PopStyleColor();
                }
                else if (testReqPending)
                {
                    ImGui.Text(" . . .");
                }
                else
                {
                    ImGui.NewLine();
                }
                ImGui.NewLine();
                ImGui.SetCursorPosX(cx);
                ImGui.PushFont(UiBuilder.IconFont);
                if (ImGui.Button(FontAwesomeIcon.Times.ToIconString()))
                {
                    this.IsVisible = false;
                }
                ImGui.PopFont(); 
                ImGui.SameLine();
                if (ImGui.Checkbox("Experimental: snapshot target's actor obj##snapshot", ref SnapshotActor))
                {
                    config.SnapshotActorExperimental = SnapshotActor;
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.PushStyleColor(ImGuiCol.Text, grey);
                    ImGui.Text("Feature isn't implemented yet so this button doesn't really do anything\nfor now it just tells you if the person you inspect visibly has cleared an ultimate");
                    ImGui.PopStyleColor();
                    ImGui.EndTooltip();
                }
                ImGui.SameLine();
                if (!config.initialConfig && (config?.client_id == "" || config?.client_secret == ""))
                {
                    if (ImGui.Button("Redo initial setup"))
                    {
                        config.initialConfig = true;
                        this.Interface.UiBuilder.OnBuildUi += DrawInitialSetup;
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.PushTextWrapPos(250f);
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0f, 0f, 1f));
                        ImGui.TextWrapped("Click this if either your client id or secret doesn't appear in the textboxes above!\n\nIf you click this by accident just click \"Test connection to API\" to make the menu go away again.");
                        ImGui.PopStyleColor();
                        ImGui.PopTextWrapPos();
                        ImGui.EndTooltip();
                    }
                }
                else
                {
                    ImGui.Text("\n");
                }
                
                ImGui.SetCursorPosX(cx);
                cy = ImGui.GetCursorPosY() + 10;
                ImGui.SetCursorPosY(cy);
                ImGui.End();
            }
            catch (Exception) { }

        }
         
    }
}