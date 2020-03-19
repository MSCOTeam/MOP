﻿// Modern Optimization Plugin
// Copyright(C) 2019-2020 Athlon

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.If not, see<http://www.gnu.org/licenses/>.

using MSCLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace MOP
{
    class IgnoreRule
    {
        public string ObjectName;
        public bool TotalIgnore;

        public IgnoreRule(string ObjectName, bool TotalIgnore)
        {
            this.ObjectName = ObjectName;
            this.TotalIgnore = TotalIgnore;
        }
    }

    public enum ToggleModes { Normal, Renderer, Item, Vehicle, VehiclePhysics };

    class ToggleRule
    {
        public string ObjectName;
        public ToggleModes ToggleMode;

        public ToggleRule(string ObjectName, ToggleModes ToggleMode)
        {
            this.ObjectName = ObjectName;
            this.ToggleMode = ToggleMode;
        }
    }

    class RuleFiles
    {
        public static RuleFiles instance;

        public List<IgnoreRule> IgnoreRules;
        public List<ToggleRule> ToggleRules;        
        
        public List<IgnoreRule> YardIgnoreRules;
        public List<IgnoreRule> StoreIgnoreRules;
        public List<IgnoreRule> RepairShopIgnoreRules;
        public List<IgnoreRule> InspectionIgnoreRules;

        string mopConfigFolder;
        string lastModListPath;
        string lastDateFilePath;

        const string RemoteServer = "http://athlon.kkmr.pl/mop/rulefiles/";
        const int FileThresholdHours = 168; // 1 week

        public RuleFiles(string mopConfigFolder)
        {
            instance = this;
            IgnoreRules = new List<IgnoreRule>();
            ToggleRules = new List<ToggleRule>();
            
            YardIgnoreRules = new List<IgnoreRule>();
            StoreIgnoreRules = new List<IgnoreRule>();
            RepairShopIgnoreRules = new List<IgnoreRule>();
            InspectionIgnoreRules = new List<IgnoreRule>();

            this.mopConfigFolder = mopConfigFolder;
            lastModListPath = $"{mopConfigFolder}\\LastModList.mop";
            lastDateFilePath = $"{mopConfigFolder}\\LastUpdate.mop";

            DownloadAndUpdateRules();

            DirectoryInfo dir = new DirectoryInfo(mopConfigFolder);
            FileInfo[] files = dir.GetFiles().Where(d => d.Name.EndsWith(".mopconfig")).ToArray();
            if (files.Length == 0)
            {
                ModConsole.Print($"[MOP] No rule files found");
                return;
            }

            ModConsole.Print($"[MOP] Found {files.Length} rule file{(files.Length > 1 ? "s" : "")}!");

            List<string> modIds = new List<string>();
            foreach (var mod in ModLoader.LoadedMods)
                modIds.Add(mod.ID);

            foreach (FileInfo file in files)
            {
                // Delete rules for mods that don't exist
                if (!modIds.Contains(Path.GetFileNameWithoutExtension(file.Name)))
                {
                    File.Delete(file.FullName);
                    ModConsole.Print($"[MOP] Rule file {file.Name} has been deleted, because corresponding mod is not present.");
                    continue;
                }

                ReadRules(file.FullName);
            }

            ModConsole.Print("[MOP] Loading rule files done!");
        }


        void ReadRules(string rulePath)
        {
            // You know the rules and so do I
            // A full commitment's what I'm thinking of
            // You wouldn't get this from any other guy
            
            // I just wanna tell you how I'm feeling
            // Gotta make you understand
            
            // Never gonna give you up
            // Never gonna let you down
            // Never gonna run around and desert you
            // Never gonna make you cry
            // Never gonna say goodbye
            // Never gonna tell a lie and hurt you

            string[] content = File.ReadAllLines(rulePath).Where(s => s.Length > 0 && !s.StartsWith("##") && s.Contains(":")).ToArray();
            foreach (string s in content)
            {
                string flag = s.Split(':')[0];
                string value = s.Split(':')[1].Trim();

                switch (flag)
                {
                    default:
                        ModConsole.Error($"[MOP] Unrecognized flag '{flag}' in file {rulePath}");
                        break;
                    case "ignore":
                        IgnoreRules.Add(new IgnoreRule(value, false));
                        break;
                    case "ignore_full":
                        IgnoreRules.Add(new IgnoreRule(value, true));
                        break;
                    case "ignore_at_place":
                        string place = value.Split(' ')[0];
                        string obj = value.Split(' ')[1];
                        switch (place)
                        {
                            case "STORE":
                                StoreIgnoreRules.Add(new IgnoreRule(obj, false));
                                break;
                            case "YARD":
                                YardIgnoreRules.Add(new IgnoreRule(obj, false));
                                break;
                            case "REPAIRSHOP":
                                RepairShopIgnoreRules.Add(new IgnoreRule(obj, false));
                                break;
                            case "INSPECTION":
                                InspectionIgnoreRules.Add(new IgnoreRule(obj, false));
                                break;
                        }
                        break;
                    case "toggle":
                        ToggleRules.Add(new ToggleRule(value, ToggleModes.Normal));
                        break;
                    case "toggle_renderer":
                        ToggleRules.Add(new ToggleRule(value, ToggleModes.Renderer));
                        break;
                    case "toggle_as_item":
                        ToggleRules.Add(new ToggleRule(value, ToggleModes.Item));
                        break;
                    case "toggle_as_vehicle":
                        ToggleRules.Add(new ToggleRule(value, ToggleModes.Vehicle));
                        break;
                    case "toggle_as_vehicle_physics_only":
                        ToggleRules.Add(new ToggleRule(value, ToggleModes.VehiclePhysics));
                        break;
                }
            }
        }

        /// <summary>
        /// Downloads the rule file from server
        /// </summary>
        void DownloadAndUpdateRules()
        {
            string lastModList = File.Exists(lastModListPath) ? File.ReadAllText(lastModListPath) : "";
            Mod[] mods = ModLoader.LoadedMods.Where(m => !m.ID.ContainsAny("MSCLoader_", "MOP")).ToArray();
            string modListString = "";

            ModConsole.Print("[MOP] Downloading rules...");

            bool isUpdateTime = !File.Exists(lastDateFilePath) || IsUpdateTime();

            foreach (Mod mod in mods)
            {
                string modId = mod.ID;
                modListString += $"{modId}\n";

                string ruleUrl = RemoteServer + modId + ".mopconfig";
                string filePath = $"{mopConfigFolder}\\{modId}.mopconfig";

                if (File.Exists(filePath) && !IsFileBelowThreshold(filePath, FileThresholdHours))
                {
                    continue;
                }
                else
                {
                    if (lastModList.Contains(mod.ID) && !isUpdateTime)
                        continue;
                }

                if (!RemoteFileExists(ruleUrl))
                    continue;

                ModConsole.Print($"<color=yellow>[MOP] Downloading new rule file for {mod.Name}...</color>");
                using (WebClient web = new WebClient())
                {
                    web.DownloadFile(ruleUrl, filePath);
                    web.Dispose();
                }
                ModConsole.Print("<color=green>[MOP] Downloading completed!</color>");
            }

            File.WriteAllText(lastModListPath, modListString);
            if (isUpdateTime)
                File.WriteAllText(lastDateFilePath, DateTime.Now.ToString());
        }

        bool RemoteFileExists(string url)
        {
            try
            {
                //Creating the HttpWebRequest
                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                //Setting the Request method HEAD, you can also use GET too.
                request.Method = "HEAD";
                //Getting the Web Response.
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                //Returns TRUE if the Status code == 200 and it's not 404.html website

                string responseUri = response.ResponseUri.ToString();
                response.Close();
                return (response.StatusCode == HttpStatusCode.OK && responseUri != "http://athlon.kkmr.pl/404.html");
            }
            catch
            {
                //Any exception will returns false.
                return false;
            }
        }

        bool IsFileBelowThreshold(string filename, int hours)
        {
            var threshold = DateTime.Now.AddHours(-hours);
            return File.GetCreationTime(filename) <= threshold;
        }

        bool IsUpdateTime()
        {
            DateTime past;
            if (DateTime.TryParse(File.ReadAllText(lastDateFilePath), out past))
            {
                past = past.AddHours(FileThresholdHours);
                return DateTime.Now > past;
            }

            return true;
        }
    }
}
