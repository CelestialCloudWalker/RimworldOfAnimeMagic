using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
using HarmonyLib;

namespace AnimeArsenal
{
    [StaticConstructorOnStartup]
    public static class DemonSlayerNameInjector
    {
        static DemonSlayerNameInjector()
        {
            Log.Message("[AnimeArsenal] Injecting Demon Slayer names into RimWorld...");
            InjectDemonSlayerNames();
        }

        private static void InjectDemonSlayerNames()
        {
            try
            {
                var nameBank = PawnNameDatabaseShuffled.BankOf(PawnNameCategory.HumanStandard);

                var maleFirstNames = new List<string>
                {
                    "Tanjiro", "Zenitsu", "Inosuke", "Giyu", "Kyojuro", "Tengen",
                    "Sanemi", "Gyomei", "Obanai", "Muichiro", "Sabito", "Genya",
                    "Sakonji", "Jigoro", "Shinjuro", "Yoriichi", "Murata", "Goto",
                    "Hotaru", "Kotetsu", "Tetsuido", "Kozo", "Tetsumushi", "Senjuro",
                    "Takeo", "Shigeru", "Rokuta", "Yahaba", "Kyogai", "Muzan",
                    "Kokushibo", "Douma", "Akaza", "Hantengu", "Gyokko", "Gyutaro",
                    "Kaigaku", "Enmu", "Rui", "Sekido", "Karaku", "Aizetsu", "Urogi",
                    "Zohakuten", "Urami", "Michikatsu", "Wakuraba", "Rokuro", "Hairo",
                    "Kamanue", "Kagaya"
                };

                var femaleFirstNames = new List<string>
                {
                    "Nezuko", "Kanao", "Shinobu", "Mitsuri", "Tamayo", "Aoi", "Makomo",
                    "Kanae", "Ruka", "Kotoha", "Rei", "Tsutako", "Kie", "Amane",
                    "Hinaki", "Nichika", "Suma", "Makio", "Hinatsuru", "Sumi",
                    "Kiyo", "Naho", "Hanako", "Kuina", "Kanata", "Koyuki", "Uta",
                    "Daki", "Nakime", "Susamaru", "Mukago", "Ubume"
                };

                var unisexFirstNames = new List<string>
                {
                    "Yushiro", "Tecchin"
                };

                var lastNames = new List<string>
                {
                    "Kamado", "Agatsuma", "Hashibira", "Tomioka", "Rengoku", "Uzui",
                    "Tokito", "Himejima", "Shinazugawa", "Iguro", "Kocho", "Kanroji",
                    "Urokodaki", "Kuwajima", "Haganezuka", "Kanamori", "Ubuyashiki",
                    "Tsuyuri", "Kanzaki", "Nakahara", "Murata", "Goto", "Sabito",
                    "Tsugikuni"
                };

                var maleNickNames = new List<string>
                {
                    "Tan", "Zen", "Ino", "Gyu", "Kyo", "Ten", "San", "Gyo", "Obi", "Mui",
                    "Gen", "Sab", "Jig", "Shin", "Yori", "Hot", "Tet", "Sen", "Take",
                    "Roku", "Muzan", "Douma", "Akaza", "Rui", "Gyutaro"
                };

                var femaleNickNames = new List<string>
                {
                    "Nez", "Shino", "Mit", "Kan", "Ao", "Tam", "Mako", "Ruka", "Kie",
                    "Ama", "Hina", "Nichi", "Su", "Maki", "Suma", "Kiyo", "Naho",
                    "Hana", "Kuina", "Koyu", "Daki", "Naki"
                };

                var unisexNickNames = new List<string>
                {
                    "Sen", "Gen"
                };

                for (int i = 0; i < 15; i++) 
                {
                    nameBank.AddNames(PawnNameSlot.First, Gender.Male, maleFirstNames);
                    nameBank.AddNames(PawnNameSlot.First, Gender.Female, femaleFirstNames);
                    nameBank.AddNames(PawnNameSlot.First, Gender.None, unisexFirstNames);

                    nameBank.AddNames(PawnNameSlot.Nick, Gender.Male, maleNickNames);
                    nameBank.AddNames(PawnNameSlot.Nick, Gender.Female, femaleNickNames);
                    nameBank.AddNames(PawnNameSlot.Nick, Gender.None, unisexNickNames);

                    nameBank.AddNames(PawnNameSlot.Last, Gender.None, lastNames);
                }

                Log.Message($"[AnimeArsenal] Successfully added {maleFirstNames.Count + femaleFirstNames.Count + unisexFirstNames.Count} first names, {lastNames.Count} last names, and {maleNickNames.Count + femaleNickNames.Count + unisexNickNames.Count} nicknames from Demon Slayer!");
            }
            catch (Exception ex)
            {
                Log.Error($"[AnimeArsenal] Failed to inject Demon Slayer names: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "GeneratePawnName")]
    public static class PawnNameGenerator_Patch
    {
        static void Postfix(ref Name __result, Pawn pawn, NameStyle style = NameStyle.Full, string forcedLastName = null)
        {
        }
    }

    public class AnimeArsenalSettings : ModSettings
    {
        public static float demonSlayerNameChance = 0.1f; 
        public static bool enableDemonSlayerNames = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref demonSlayerNameChance, "demonSlayerNameChance", 0.1f);
            Scribe_Values.Look(ref enableDemonSlayerNames, "enableDemonSlayerNames", true);
        }
    }

    public class AnimeArsenalMod : Mod
    {
        private AnimeArsenalSettings settings;

        public AnimeArsenalMod(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<AnimeArsenalSettings>();
        }

        public override string SettingsCategory()
        {
            return "Anime Arsenal";
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.CheckboxLabeled("Enable Demon Slayer Names", ref AnimeArsenalSettings.enableDemonSlayerNames);
            listingStandard.Label($"Demon Slayer Name Frequency: {AnimeArsenalSettings.demonSlayerNameChance:P0}");
            AnimeArsenalSettings.demonSlayerNameChance = listingStandard.Slider(AnimeArsenalSettings.demonSlayerNameChance, 0f, 1f);

            listingStandard.End();
            base.WriteSettings();
        }
    }
}