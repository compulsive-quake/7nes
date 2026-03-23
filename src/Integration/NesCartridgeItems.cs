using System.IO;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace SevenNes.Integration
{
    /// <summary>
    /// Dynamically generates item definitions and icons for each ROM in the Roms folder.
    /// Called during InitMod (before the game loads Config XMLs), so generated files
    /// are picked up by the normal config loading pipeline.
    /// </summary>
    public static class NesCartridgeItems
    {
        /// <summary>
        /// Strips region/variant tags in parentheses and brackets from a ROM filename.
        /// "Contra (USA)" → "Contra", "Super Mario Bros. (World)" → "Super Mario Bros."
        /// </summary>
        public static string GetCleanName(string romFileName)
        {
            string name = Path.GetFileNameWithoutExtension(romFileName);
            name = Regex.Replace(name, @"\s*[\(\[].*?[\)\]]", "").Trim();
            return name;
        }

        /// <summary>
        /// Creates a valid 7DTD item name from a ROM filename.
        /// "Contra (USA).nes" → "nesCart_ContraUSA"
        /// </summary>
        public static string GetItemName(string romFileName)
        {
            string name = Path.GetFileNameWithoutExtension(romFileName);
            string sanitized = Regex.Replace(name, @"[^a-zA-Z0-9]", "");
            return "nesCart_" + sanitized;
        }

        /// <summary>
        /// Scans the Roms folder and generates Config/items.xml, localization entries,
        /// and copies box art icons so the game picks them up during normal loading.
        /// </summary>
        public static void GenerateItemConfigs(string modPath, string romsPath)
        {
            if (!Directory.Exists(romsPath))
                return;

            var romFiles = Directory.GetFiles(romsPath, "*.nes");
            if (romFiles.Length == 0)
            {
                // Remove items.xml if no ROMs (clean up stale items)
                var itemsXmlPath = Path.Combine(modPath, "Config", "items.xml");
                if (File.Exists(itemsXmlPath))
                    File.Delete(itemsXmlPath);
                return;
            }

            GenerateItemsXml(modPath, romFiles);
            UpdateLocalization(modPath, romFiles);
            PrepareIcons(modPath, romsPath, romFiles);

            Log.Out($"[7nes] Generated item configs for {romFiles.Length} ROMs");
        }

        private static void GenerateItemsXml(string modPath, string[] romFiles)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<configs>");
            sb.AppendLine("\t<append xpath=\"/items\">");

            foreach (var romFile in romFiles)
            {
                string itemName = GetItemName(romFile);
                string cleanName = SecurityElement.Escape(GetCleanName(romFile));

                sb.AppendLine($"\t\t<item name=\"{itemName}\">");
                sb.AppendLine($"\t\t\t<property name=\"Meshfile\" value=\"Items/Misc/oilGP\"/>");
                sb.AppendLine($"\t\t\t<property name=\"DropMeshfile\" value=\"Items/Misc/sacPT\"/>");
                sb.AppendLine($"\t\t\t<property name=\"CustomIcon\" value=\"{itemName}\"/>");
                sb.AppendLine($"\t\t\t<property name=\"CustomIconTint\" value=\"255,255,255\"/>");
                sb.AppendLine($"\t\t\t<property name=\"Stacknumber\" value=\"1\"/>");
                sb.AppendLine($"\t\t\t<property name=\"CreativeMode\" value=\"Player\"/>");
                sb.AppendLine($"\t\t\t<property name=\"Group\" value=\"Decor/Miscellaneous\"/>");
                sb.AppendLine($"\t\t\t<property name=\"DescriptionKey\" value=\"{itemName}_desc\"/>");
                sb.AppendLine($"\t\t\t<property name=\"Material\" value=\"Mplastics\"/>");
                sb.AppendLine($"\t\t</item>");
            }

            sb.AppendLine("\t</append>");
            sb.AppendLine("</configs>");

            var configDir = Path.Combine(modPath, "Config");
            Directory.CreateDirectory(configDir);
            File.WriteAllText(Path.Combine(configDir, "items.xml"), sb.ToString());
        }

        private static void UpdateLocalization(string modPath, string[] romFiles)
        {
            var locPath = Path.Combine(modPath, "Config", "localization.txt");
            if (!File.Exists(locPath))
                return;

            // Read existing lines, strip any previously generated nesCart entries
            var lines = new StringBuilder();
            foreach (var line in File.ReadAllLines(locPath))
            {
                if (!line.StartsWith("nesCart"))
                    lines.AppendLine(line);
            }

            // Append fresh entries for each ROM
            foreach (var romFile in romFiles)
            {
                string itemName = GetItemName(romFile);
                string cleanName = GetCleanName(romFile);

                // Escape commas for CSV
                if (cleanName.Contains(","))
                    cleanName = "\"" + cleanName + "\"";

                lines.AppendLine($"{itemName},items,Item,FALSE,FALSE,{cleanName}");
                lines.AppendLine($"{itemName}_desc,items,Item,FALSE,FALSE,NES Game Cartridge: {cleanName}");
            }

            File.WriteAllText(locPath, lines.ToString().TrimEnd() + "\n");
        }

        private static void PrepareIcons(string modPath, string romsPath, string[] romFiles)
        {
            var iconDir = Path.Combine(modPath, "UIAtlases", "ItemIconAtlas");
            Directory.CreateDirectory(iconDir);

            var boxArtDir = Path.Combine(romsPath, "box");
            var defaultIconPath = Path.Combine(iconDir, "nesCartridge_default.png");
            byte[] defaultIcon = null;

            if (File.Exists(defaultIconPath))
                defaultIcon = File.ReadAllBytes(defaultIconPath);

            foreach (var romFile in romFiles)
            {
                string romName = Path.GetFileNameWithoutExtension(romFile);
                string itemName = GetItemName(romFile);
                string destPath = Path.Combine(iconDir, itemName + ".png");

                // Check for box art matching the ROM name
                bool found = false;
                if (Directory.Exists(boxArtDir))
                {
                    var boxArtPath = Path.Combine(boxArtDir, romName + ".png");
                    if (File.Exists(boxArtPath))
                    {
                        File.Copy(boxArtPath, destPath, true);
                        found = true;
                        Log.Out($"[7nes] Using box art for: {romName}");
                    }
                }

                // Fall back to default icon
                if (!found && defaultIcon != null)
                {
                    File.WriteAllBytes(destPath, defaultIcon);
                }
            }
        }
    }
}
