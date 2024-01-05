using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using System.Text;
using System.Text.Json;
using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace SharpTimer
{
    public partial class SharpTimer
    {
        private async void ServerRecordADtimer()
        {
            if (isADTimerRunning) return;

            var timer = AddTimer(srTimer, async () =>
            {
                SharpTimerDebug($"Running Server Record AD...");
                Dictionary<string, PlayerRecord> sortedRecords;
                if (useMySQL == false)
                {
                    SharpTimerDebug($"Getting Server Record AD using json");
                    sortedRecords = GetSortedRecords();
                }
                else
                {
                    SharpTimerDebug($"Getting Server Record AD using datanase");
                    sortedRecords = await GetSortedRecordsFromDatabase();
                }

                if (sortedRecords.Count == 0)
                {
                    SharpTimerDebug($"No Server Records for this map yet!");
                    return;
                }

                Server.NextFrame(() => Server.PrintToChatAll($"{msgPrefix} Current Server Record on {ParseColorToSymbol(primaryHUDcolor)}{currentMapName}{ChatColors.White}: "));

                foreach (var kvp in sortedRecords.Take(1))
                {
                    string playerName = kvp.Value.PlayerName; // Get the player name from the dictionary value
                    int timerTicks = kvp.Value.TimerTicks; // Get the timer ticks from the dictionary value

                    Server.NextFrame(() => Server.PrintToChatAll(msgPrefix + $" {ParseColorToSymbol(primaryHUDcolor)}{playerName} {ChatColors.White}- {ParseColorToSymbol(primaryHUDcolor)}{FormatTime(timerTicks)}"));
                }

            }, TimerFlags.REPEAT);
            isADTimerRunning = true;
        }

        public void SharpTimerDebug(string msg)
        {
            if (enableDebug == true) Console.WriteLine($"\u001b[33m[SharpTimerDebug] \u001b[37m{msg}");
        }

        public void SharpTimerError(string msg)
        {
            Console.WriteLine($"\u001b[31m[SharpTimerERROR] \u001b[37m{msg}");
        }

        public void SharpTimerConPrint(string msg)
        {
            Console.WriteLine($"\u001b[36m[SharpTimer] \u001b[37m{msg}");
        }

        private string GetSpeedBar(double speed)
        {
            const int barLength = 80;

            int barProgress = (int)Math.Round((speed / altVeloMaxSpeed) * barLength);
            StringBuilder speedBar = new StringBuilder(barLength);

            for (int i = 0; i < barLength; i++)
            {
                if (i < barProgress)
                {
                    speedBar.Append($"<font class='fontSize-s' color='{(speed >= altVeloMaxSpeed ? GetRainbowColor() : primaryHUDcolor)}'>|</font>");
                }
                else
                {
                    speedBar.Append($"<font class='fontSize-s' color='{secondaryHUDcolor}'>|</font>");
                }
            }

            return $"{speedBar}<br>";
        }

        private string GetRainbowColor()
        {
            const double rainbowPeriod = 2.0;

            double percentage = (Server.EngineTime % rainbowPeriod) / rainbowPeriod;
            double red = Math.Sin(2 * Math.PI * (percentage)) * 127 + 128;
            double green = Math.Sin(2 * Math.PI * (percentage + 1.0 / 3.0)) * 127 + 128;
            double blue = Math.Sin(2 * Math.PI * (percentage + 2.0 / 3.0)) * 127 + 128;

            int intRed = (int)Math.Round(red);
            int intGreen = (int)Math.Round(green);
            int intBlue = (int)Math.Round(blue);

            return $"#{intRed:X2}{intGreen:X2}{intBlue:X2}";
        }

        private static string FormatTime(int ticks)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(ticks / 64.0);

            string milliseconds = $"{(ticks % 64) * (1000.0 / 64.0):000}";

            int totalMinutes = (int)timeSpan.TotalMinutes;
            if (totalMinutes >= 60)
            {
                return $"{totalMinutes / 60:D1}:{totalMinutes % 60:D2}:{timeSpan.Seconds:D2}.{milliseconds}";
            }

            return $"{totalMinutes:D1}:{timeSpan.Seconds:D2}.{milliseconds}";
        }

        private static string FormatTimeDifference(int currentTicks, int previousTicks)
        {
            int differenceTicks = previousTicks - currentTicks;
            string sign = (differenceTicks > 0) ? "-" : "+";
            char signColor = (differenceTicks > 0) ? ChatColors.Green : ChatColors.Red;

            TimeSpan timeDifference = TimeSpan.FromSeconds(Math.Abs(differenceTicks) / 64.0);

            // Format seconds with three decimal points
            string secondsWithMilliseconds = $"{timeDifference.Seconds:D2}.{(Math.Abs(differenceTicks) % 64) * (1000.0 / 64.0):000}";

            int totalDifferenceMinutes = (int)timeDifference.TotalMinutes;
            if (totalDifferenceMinutes >= 60)
            {
                return $"{signColor}{sign}{totalDifferenceMinutes / 60:D1}:{totalDifferenceMinutes % 60:D2}:{secondsWithMilliseconds}";
            }

            return $"{signColor}{sign}{totalDifferenceMinutes:D1}:{secondsWithMilliseconds}";
        }

        static string StringAfterPrefix(string input, string prefix)
        {
            int prefixIndex = input.IndexOf(prefix);
            if (prefixIndex != -1)
            {
                int startIndex = prefixIndex + prefix.Length;
                string result = input.Substring(startIndex);
                return result;
            }
            return string.Empty;
        }

        string ParseColorToSymbol(string input)
        {
            Dictionary<string, string> colorNameSymbolMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
             {
                 { "white", "" },
                 { "darkred", "" },
                 { "purple", "" },
                 { "darkgreen", "" },
                 { "lightgreen", "" },
                 { "green", "" },
                 { "red", "" },
                 { "lightgray", "" },
                 { "orange", "" },
                 { "darkpurple", "" },
                 { "lightred", "" }
             };

            string lowerInput = input.ToLower();

            if (colorNameSymbolMap.TryGetValue(lowerInput, out var symbol))
            {
                return symbol;
            }

            if (IsHexColorCode(input))
            {
                return ParseHexToSymbol(input);
            }

            return "\u0010";
        }

        bool IsHexColorCode(string input)
        {
            if (input.StartsWith("#") && (input.Length == 7 || input.Length == 9))
            {
                try
                {
                    Color color = ColorTranslator.FromHtml(input);
                    return true;
                }
                catch (Exception ex)
                {
                    SharpTimerError($"Error parsing hex color code: {ex.Message}");
                }
            }
            else
            {
                SharpTimerError("Invalid hex color code format. Please check SharpTimer/config.cfg");
            }

            return false;
        }

        static string ParseHexToSymbol(string hexColorCode)
        {
            Color color = ColorTranslator.FromHtml(hexColorCode);

            Dictionary<string, string> predefinedColors = new Dictionary<string, string>
            {
                { "#FFFFFF", "" },  // White
                { "#8B0000", "" },  // Dark Red
                { "#800080", "" },  // Purple
                { "#006400", "" },  // Dark Green
                { "#00FF00", "" },  // Light Green
                { "#008000", "" },  // Green
                { "#FF0000", "" },  // Red
                { "#D3D3D3", "" },  // Light Gray
                { "#FFA500", "" },  // Orange
                { "#780578", "" },  // Dark Purple
                { "#FF4500", "" }   // Light Red
            };

            hexColorCode = hexColorCode.ToUpper();

            if (predefinedColors.TryGetValue(hexColorCode, out var colorName))
            {
                return colorName;
            }

            Color targetColor = ColorTranslator.FromHtml(hexColorCode);
            string closestColor = FindClosestColor(targetColor, predefinedColors.Keys);

            if (predefinedColors.TryGetValue(closestColor, out var symbol))
            {
                return symbol;
            }

            return "";
        }

        static string FindClosestColor(Color targetColor, IEnumerable<string> colorHexCodes)
        {
            double minDistance = double.MaxValue;
            string closestColor = null;

            foreach (var hexCode in colorHexCodes)
            {
                Color color = ColorTranslator.FromHtml(hexCode);
                double distance = ColorDistance(targetColor, color);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestColor = hexCode;
                }
            }

            return closestColor;
        }

        static double ColorDistance(Color color1, Color color2)
        {
            int rDiff = color1.R - color2.R;
            int gDiff = color1.G - color2.G;
            int bDiff = color1.B - color2.B;

            return Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
        }

        public void DrawLaserBetween(Vector startPos, Vector endPos)
        {
            CBeam beam = Utilities.CreateEntityByName<CBeam>("beam");
            if (beam == null)
            {
                SharpTimerDebug($"Failed to create beam...");
                return;
            }

            if (IsHexColorCode(primaryHUDcolor))
            {
                beam.Render = ColorTranslator.FromHtml(primaryHUDcolor);
            }
            else
            {
                beam.Render = Color.FromName(primaryHUDcolor);
            }

            beam.Width = 1.5f;

            beam.Teleport(startPos, new QAngle(0, 0, 0), new Vector(0, 0, 0));

            beam.EndPos.X = endPos.X;
            beam.EndPos.Y = endPos.Y;
            beam.EndPos.Z = endPos.Z;

            beam.DispatchSpawn();
            SharpTimerDebug($"Beam Spawned at S:{startPos} E:{beam.EndPos}");
        }

        public void DrawWireframe2D(Vector corner1, Vector corner2, float height = 50)
        {
            Vector corner3 = new Vector(corner2.X, corner1.Y, corner1.Z);
            Vector corner4 = new Vector(corner1.X, corner2.Y, corner1.Z);

            Vector corner1_top = new Vector(corner1.X, corner1.Y, corner1.Z + height);
            Vector corner2_top = new Vector(corner2.X, corner2.Y, corner2.Z + height);
            Vector corner3_top = new Vector(corner2.X, corner1.Y, corner1.Z + height);
            Vector corner4_top = new Vector(corner1.X, corner2.Y, corner1.Z + height);

            DrawLaserBetween(corner1, corner3);
            DrawLaserBetween(corner1, corner4);
            DrawLaserBetween(corner2, corner3);
            DrawLaserBetween(corner2, corner4);

            DrawLaserBetween(corner1_top, corner3_top);
            DrawLaserBetween(corner1_top, corner4_top);
            DrawLaserBetween(corner2_top, corner3_top);
            DrawLaserBetween(corner2_top, corner4_top);

            DrawLaserBetween(corner1, corner1_top);
            DrawLaserBetween(corner2, corner2_top);
            DrawLaserBetween(corner3, corner3_top);
            DrawLaserBetween(corner4, corner4_top);
        }

        public void DrawWireframe3D(Vector corner1, Vector corner8)
        {
            Vector corner2 = new Vector(corner1.X, corner8.Y, corner1.Z);
            Vector corner3 = new Vector(corner8.X, corner8.Y, corner1.Z);
            Vector corner4 = new Vector(corner8.X, corner1.Y, corner1.Z);

            Vector corner5 = new Vector(corner8.X, corner1.Y, corner8.Z);
            Vector corner6 = new Vector(corner1.X, corner1.Y, corner8.Z);
            Vector corner7 = new Vector(corner1.X, corner8.Y, corner8.Z);

            //top square
            DrawLaserBetween(corner1, corner2);
            DrawLaserBetween(corner2, corner3);
            DrawLaserBetween(corner3, corner4);
            DrawLaserBetween(corner4, corner1);

            //bottom square
            DrawLaserBetween(corner5, corner6);
            DrawLaserBetween(corner6, corner7);
            DrawLaserBetween(corner7, corner8);
            DrawLaserBetween(corner8, corner5);

            //connect them both to build a cube
            DrawLaserBetween(corner1, corner6);
            DrawLaserBetween(corner2, corner7);
            DrawLaserBetween(corner3, corner8);
            DrawLaserBetween(corner4, corner5);
        }

        private bool IsVectorInsideBox(Vector playerVector, Vector corner1, Vector corner2)
        {
            float minX = Math.Min(corner1.X, corner2.X);
            float minY = Math.Min(corner1.Y, corner2.Y);
            float minZ = Math.Min(corner1.Z, corner2.Z);

            float maxX = Math.Max(corner1.X, corner2.X);
            float maxY = Math.Max(corner1.Y, corner2.Y);
            float maxZ = Math.Max(corner1.Z, corner1.Z);

            return playerVector.X >= minX && playerVector.X <= maxX &&
                   playerVector.Y >= minY && playerVector.Y <= maxY &&
                   playerVector.Z >= minZ && playerVector.Z <= maxZ + fakeTriggerHeight;
        }

        private static Vector ParseVector(string vectorString)
        {
            const char separator = ' ';

            var values = vectorString.Split(separator);

            if (values.Length == 3 &&
                float.TryParse(values[0], out float x) &&
                float.TryParse(values[1], out float y) &&
                float.TryParse(values[2], out float z))
            {
                return new Vector(x, y, z);
            }

            return new Vector(0, 0, 0);
        }

        private static QAngle ParseQAngle(string qAngleString)
        {
            const char separator = ' ';

            var values = qAngleString.Split(separator);

            if (values.Length == 3 &&
                float.TryParse(values[0], out float pitch) &&
                float.TryParse(values[1], out float yaw) &&
                float.TryParse(values[2], out float roll))
            {
                return new QAngle(pitch, yaw, roll);
            }

            return new QAngle(0, 0, 0);
        }

        public Dictionary<string, PlayerRecord> GetSortedRecords(int bonusX = 0)
        {
            string currentMapName = bonusX == 0 ? Server.MapName : $"{Server.MapName}_bonus{bonusX}";

            Dictionary<string, Dictionary<string, PlayerRecord>> records;
            if (File.Exists(playerRecordsPath))
            {
                string json = File.ReadAllText(playerRecordsPath);
                records = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, PlayerRecord>>>(json) ?? new Dictionary<string, Dictionary<string, PlayerRecord>>();
            }
            else
            {
                records = new Dictionary<string, Dictionary<string, PlayerRecord>>();
            }

            if (records.ContainsKey(currentMapName))
            {
                var sortedRecords = records[currentMapName]
                    .OrderBy(record => record.Value.TimerTicks)
                    .ToDictionary(record => record.Key, record => new PlayerRecord
                    {
                        PlayerName = record.Value.PlayerName,
                        TimerTicks = record.Value.TimerTicks
                    });

                return sortedRecords;
            }

            return new Dictionary<string, PlayerRecord>();
        }

        private async Task<(int? Tier, string? Type)> FineMapInfoFromHTTP(string url)
        {
            try
            {
                SharpTimerDebug($"Trying to fetch remote_data for {currentMapName} from {url}");
                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetStringAsync(url);
                    var jsonDocument = JsonDocument.Parse(response);

                    if (jsonDocument.RootElement.TryGetProperty(currentMapName, out var mapInfo))
                    {
                        int? tier = null;
                        string? type = null;

                        if (mapInfo.TryGetProperty("Tier", out var tierElement))
                        {
                            tier = tierElement.GetInt32();
                        }

                        if (mapInfo.TryGetProperty("Type", out var typeElement))
                        {
                            type = typeElement.GetString();
                        }

                        SharpTimerDebug($"Fetched remote_data success! {tier} {type}");

                        return (tier, type);
                    }
                }

                return (null, null);
            }
            catch (Exception ex)
            {
                SharpTimerError($"Error Getting Remote Data for {currentMapName}: {ex.Message}");
                return (null, null);
            }
        }

        private async Task AddMapInfoToHostname()
        {
            string mapInfoSource = GetMapInfoSource();
            var (mapTier, mapType) = await FineMapInfoFromHTTP(mapInfoSource);
            currentMapTier = mapTier;
            currentMapType = mapType;
            string tierString = currentMapTier != null ? $" | Tier: {currentMapTier}" : "";
            string typeString = currentMapType != null ? $" | {currentMapType}" : "";

            if (!autosetHostname) return;

            Server.NextFrame(() =>
            {
                Server.ExecuteCommand($"hostname {defaultServerHostname}{tierString}{typeString}");
                SharpTimerDebug($"SharpTimer Hostname Updated to: {ConVar.Find("hostname").StringValue}");
            });
        }

        private string GetMapInfoSource()
        {
            return currentMapName switch
            {
                var name when name.StartsWith("kz_") => remoteKZDataSource,
                var name when name.StartsWith("bhop_") => remoteBhopDataSource,
                var name when name.StartsWith("surf_") => remoteSurfDataSource,
                _ => null
            };
        }

        private void OnMapStartHandler(string mapName)
        {
            Server.NextFrame(() =>
            {
                SharpTimerDebug("OnMapStart:");
                SharpTimerDebug("Executing SharpTimer/config");
                Server.ExecuteCommand("sv_autoexec_mapname_cfg 0");
                Server.ExecuteCommand($"execifexists SharpTimer/config.cfg");

                //delay custom_exec so it executes after map exec
                SharpTimerDebug("Creating custom_exec 2sec delay");
                var custom_exec_delay = AddTimer(2.0f, () =>
                {
                    SharpTimerDebug("Executing SharpTimer/custom_exec");
                    Server.ExecuteCommand("execifexists SharpTimer/custom_exec.cfg");
                });

                if (removeCrouchFatigueEnabled == true) Server.ExecuteCommand("sv_timebetweenducks 0");

                bonusRespawnPoses.Clear();
                bonusRespawnAngs.Clear();

                cpTriggers.Clear();         // make sure old data is flushed in case new map uses fake zones
                stageTriggers.Clear();
                stageTriggerAngs.Clear();
                stageTriggerPoses.Clear();
            });
        }

        private void LoadMapData()
        {
            Server.ExecuteCommand($"hostname {defaultServerHostname}");

            if (srEnabled == true) ServerRecordADtimer();

            string recordsFileName = "SharpTimer/player_records.json";
            playerRecordsPath = Path.Join(gameDir + "/csgo/cfg", recordsFileName);

            string mysqlConfigFileName = "SharpTimer/mysqlConfig.json";
            mySQLpath = Path.Join(gameDir + "/csgo/cfg", mysqlConfigFileName);

            currentMapName = Server.MapName;

            string mapdataFileName = $"SharpTimer/MapData/{currentMapName}.json";
            string mapdataPath = Path.Join(gameDir + "/csgo/cfg", mapdataFileName);

            currentMapTier = null; //making sure previous map tier and type are wiped
            currentMapType = null;

            _ = AddMapInfoToHostname();

            if (triggerPushFixEnabled == true) FindTriggerPushData();

            if (File.Exists(mapdataPath))
            {
                string json = File.ReadAllText(mapdataPath);
                var mapInfo = JsonSerializer.Deserialize<MapInfo>(json);
                SharpTimerConPrint($"Map data json found for map: {currentMapName}!");

                if (!string.IsNullOrEmpty(mapInfo.MapStartC1) && !string.IsNullOrEmpty(mapInfo.MapStartC2) && !string.IsNullOrEmpty(mapInfo.MapEndC1) && !string.IsNullOrEmpty(mapInfo.MapEndC2))
                {
                    currentMapStartC1 = ParseVector(mapInfo.MapStartC1);
                    currentMapStartC2 = ParseVector(mapInfo.MapStartC2);
                    currentMapEndC1 = ParseVector(mapInfo.MapEndC1);
                    currentMapEndC2 = ParseVector(mapInfo.MapEndC2);
                    useTriggers = false;
                    SharpTimerConPrint($"Found Fake Trigger Corners: START {currentMapStartC1}, {currentMapStartC2} | END {currentMapEndC1}, {currentMapEndC2}");
                }

                if (!string.IsNullOrEmpty(mapInfo.MapStartTrigger) && !string.IsNullOrEmpty(mapInfo.MapEndTrigger))
                {
                    currentMapStartTrigger = mapInfo.MapStartTrigger;
                    currentMapEndTrigger = mapInfo.MapEndTrigger;
                    useTriggers = true;
                    SharpTimerConPrint($"Found Trigger Names: START {currentMapStartTrigger} | END {currentMapEndTrigger}");
                }

                if (!string.IsNullOrEmpty(mapInfo.RespawnPos))
                {
                    currentRespawnPos = ParseVector(mapInfo.RespawnPos);
                    SharpTimerConPrint($"Found RespawnPos: {currentRespawnPos}");
                }
                else
                {
                    (currentRespawnPos, currentRespawnAng) = FindStartTriggerPos();

                    FindBonusStartTriggerPos();
                    FindStageTriggers();
                    FindCheckpointTriggers();
                    SharpTimerConPrint($"RespawnPos not found, trying to hook trigger pos instead");
                    if (currentRespawnPos == null)
                    {
                        SharpTimerConPrint($"Hooking Trigger RespawnPos Failed!");
                    }
                    else
                    {
                        SharpTimerConPrint($"Hooking Trigger RespawnPos Success! {currentRespawnPos}");
                    }
                }

                if (!string.IsNullOrEmpty(mapInfo.OverrideDisableTelehop))
                {
                    try
                    {
                        currentMapOverrideDisableTelehop = bool.Parse(mapInfo.OverrideDisableTelehop);
                        SharpTimerConPrint($"Overriding Telehop...");
                    }
                    catch (FormatException)
                    {
                        Console.WriteLine("Invalid boolean string format for OverrideDisableTelehop");
                    }
                }
                else
                {
                    currentMapOverrideDisableTelehop = false;
                }
            }
            else
            {
                SharpTimerConPrint($"Map data json not found for map: {currentMapName}!");
                SharpTimerConPrint($"Trying to hook Triggers supported by default!");
                (currentRespawnPos, currentRespawnAng) = FindStartTriggerPos();
                FindBonusStartTriggerPos();
                FindStageTriggers();
                FindCheckpointTriggers();
                if (currentRespawnPos == null)
                {
                    SharpTimerConPrint($"Hooking Trigger RespawnPos Failed!");
                }
                else
                {
                    SharpTimerConPrint($"Hooking Trigger RespawnPos Success! {currentRespawnPos}");
                }
                useTriggers = true;
            }

            if (useTriggers == false)
            {
                DrawWireframe2D(currentMapStartC1, currentMapStartC2, fakeTriggerHeight);
                DrawWireframe2D(currentMapEndC1, currentMapEndC2, fakeTriggerHeight);
            }
            else
            {
                var (startRight, startLeft, endRight, endLeft) = FindTriggerCorners();

                if (startRight == null || startLeft == null || endRight == null || endLeft == null) return;

                DrawWireframe3D(startRight, startLeft);
                DrawWireframe3D(endRight, endLeft);
            }
        }
    }
}