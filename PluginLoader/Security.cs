﻿using avaness.PluginLoader.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace avaness.PluginLoader
{
    internal static class Security
    {
        private readonly static HashSet<ulong> whitelistItemIds = new HashSet<ulong>
        {
            // Workshop
            2292390607, // Tool Switcher
            2413859055, // SteamWorkshopFix
            2413918072, // SEWorldGenPlugin v2
            2414532651, // DecalFixPlugin
            // SEPM - Most of these are old or broken
            2004495632, // BlockPicker
            1937528740, // GridFilter
            2029854486, // RemovePlanetSizeLimits 
            2171994463, // ClientFixes
            2156683844, // SEWorldGenPlugin
            1937530079, // Mass Rename
            2037606896, // CameraLCD
        };

        private readonly static HashSet<string> whitelistItemSha = new HashSet<string>()
        {
            
        };

        public static bool Validate(ulong steamId, string file, out string sha256)
        {
            sha256 = null;
            if (whitelistItemIds.Contains(steamId))
                return true;
            sha256 = LoaderTools.GetHash256(file);
            return whitelistItemSha.Contains(sha256);
        }
    }
}
