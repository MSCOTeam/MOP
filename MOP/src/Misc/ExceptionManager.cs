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

using System;
using System.IO;
using System.Diagnostics;
using MSCLoader;

namespace MOP
{
    class ExceptionManager
    {
        /// <summary>
        /// Creates then new error dump file
        /// </summary>
        /// <param name="ex"></param>
        public static void New(Exception ex)
        {
            if (File.Exists("MOP_LOG.txt"))
                File.Delete("MOP_LOG.txt");

            string gameInfo = GetGameInfo();
            string errorInfo = ex.Message + "\n" + ex.StackTrace + "\nTarget Site: " + ex.TargetSite;

            using (StreamWriter sw = new StreamWriter("MOP_LOG.txt"))
            {
                sw.Write(gameInfo + "\n=== ERROR ===\n\n" + errorInfo);
                sw.Close();
            }

            ModConsole.Error("[MOP] An error has occured. " +
                "Log has been saved in My Summer Car folder into MOP_LOG.txt\n\n" + errorInfo);
        }

        public static void Open()
        {
            if (File.Exists("MOP_LOG.txt"))
            {
                Process.Start("MOP_LOG.txt");
            }
        }

        /// <summary>
        /// Dumps the info about the mod and lists all installed mods into MOP_REPORT.txt
        /// </summary>
        public static void GenerateReport()
        {
            string gameInfo = GetGameInfo();

            using (StreamWriter sw = new StreamWriter("MOP_REPORT.txt"))
            {
                sw.Write(gameInfo);
                sw.Close();
            }

            ModConsole.Print("[MOP] Mod report has been generated.");
            Process.Start("MOP_REPORT.txt");
        }

        /// <summary>
        /// Generates the report about mod's settings and list of installed mods
        /// </summary>
        /// <returns></returns>
        static string GetGameInfo()
        {
            string output = "MSC Mod Loader Version: " + ModLoader.MSCLoader_Ver + "\n";
            output += "Date and Time: " + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ssZ") + "\n\n";

            output += "=== MOP SETTINGS ===\n\n";

            output += "ActiveDistance: " + MopSettings.ActiveDistance + "\n";
            output += "ActiveDistanceMultiplicationValue: " + MopSettings.ActiveDistanceMultiplicationValue + "\n";
            output += "SafeMode: " + MopSettings.SafeMode.ToString() + "\n";
            output += "RemoveEmptyBeerBottles: " + MopSettings.RemoveEmptyBeerBottles.ToString() + "\n";
            output += "SatsumaTogglePhysicsOnly: " + MopSettings.SatsumaTogglePhysicsOnly.ToString() + "\n";
            output += "OverridePhysicsToggling: " + MopSettings.OverridePhysicsToggling.ToString() + "\n";
            output += "ToggleVehicles: " + MopSettings.ToggleVehicles.ToString() + "\n";
            output += "ToggleItems: " + MopSettings.ToggleItems.ToString() + "\n";
            output += "ToggleVehiclePhysicsOnly: " + MopSettings.ToggleVehiclePhysicsOnly.ToString() + "\n";
            output += "IgnoreModVehicles: " + MopSettings.IgnoreModVehicles.ToString() + "\n";

            // List installed mods
            output += "\n=== MODS ====\n\n";
            foreach (var mod in ModLoader.LoadedMods)
            {
                if (mod.ID == "MOP")
                {
                    output = $"{mod.Name}\nVersion: {mod.Version}\n" + output;
                    continue;
                }

                // Ignore MSCLoader components
                if (mod.ID == "MSCLoader_Console" || mod.ID == "MSCLoader_Settings") 
                    continue;

                output += $"{mod.Name}:\n  ID: {mod.ID}\n  Version: {mod.Version}\n  Author: {mod.Author}\n\n";
            }

            return output;
        }
    }
}