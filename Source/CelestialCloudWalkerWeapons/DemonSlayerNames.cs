using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using RimWorld;
using Verse;
using HarmonyLib;

namespace AnimeArsenal
{
    [StaticConstructorOnStartup]
    public static class DemonSlayerNameInjector
    {
        private static List<string> maleFirstNames;
        private static List<string> femaleFirstNames;
        private static List<string> unisexFirstNames;
        private static List<string> lastNames;
        private static List<string> maleNickNames;
        private static List<string> femaleNickNames;
        private static List<string> unisexNickNames;

        static DemonSlayerNameInjector()
        {
            if (!AnimeArsenalSettings.enableDemonSlayerNames)
            {
                Log.Message("[AnimeArsenal] Demon Slayer names are disabled in settings.");
                return;
            }

            Log.Message("[AnimeArsenal] Preparing Demon Slayer names...");
            InitializeNameLists();
        }

        private static void InitializeNameLists()
        {
            maleFirstNames = new List<string>
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

            femaleFirstNames = new List<string>
            {
                "Nezuko", "Kanao", "Shinobu", "Mitsuri", "Tamayo", "Aoi", "Makomo",
                "Kanae", "Ruka", "Kotoha", "Rei", "Tsutako", "Kie", "Amane",
                "Hinaki", "Nichika", "Suma", "Makio", "Hinatsuru", "Sumi",
                "Kiyo", "Naho", "Hanako", "Kuina", "Kanata", "Koyuki", "Uta",
                "Daki", "Nakime", "Susamaru", "Mukago", "Ubume"
            };

            unisexFirstNames = new List<string>
            {
                "Yushiro", "Tecchin", "Nowa"
            };

            lastNames = new List<string>
            {
                "Kamado", "Agatsuma", "Hashibira", "Tomioka", "Rengoku", "Uzui",
                "Tokito", "Himejima", "Shinazugawa", "Iguro", "Kocho", "Kanroji",
                "Urokodaki", "Kuwajima", "Haganezuka", "Kanamori", "Ubuyashiki",
                "Tsuyuri", "Kanzaki", "Nakahara", "Murata", "Goto", "Sabito",
                "Tsugikuni"
            };

            maleNickNames = new List<string>
            {
                "Tan", "Zen", "Ino", "Gyu", "Kyo", "Ten", "San", "Gyo", "Obi", "Mui",
                "Gen", "Sab", "Jig", "Shin", "Yori", "Hot", "Tet", "Sen", "Take",
                "Roku", "Muzan", "Douma", "Akaza", "Rui", "Gyutaro"
            };

            femaleNickNames = new List<string>
            {
                "Nez", "Shino", "Mit", "Kan", "Ao", "Tam", "Mako", "Ruka", "Kie",
                "Ama", "Hina", "Nichi", "Su", "Maki", "Suma", "Kiyo", "Naho",
                "Hana", "Kuina", "Koyu", "Daki", "Naki"
            };

            unisexNickNames = new List<string>
            {
                "Sen", "Gen", "Nowa"
            };

            Log.Message($"[AnimeArsenal] Loaded {maleFirstNames.Count + femaleFirstNames.Count + unisexFirstNames.Count} first names, {lastNames.Count} last names, and {maleNickNames.Count + femaleNickNames.Count + unisexNickNames.Count} nicknames!");
        }

        public static string GetRandomFirstName(Gender gender)
        {
            if (maleFirstNames == null || femaleFirstNames == null || unisexFirstNames == null)
                return null;

            if (gender == Gender.Male && maleFirstNames.Any())
                return maleFirstNames.RandomElement();
            else if (gender == Gender.Female && femaleFirstNames.Any())
                return femaleFirstNames.RandomElement();
            else if (unisexFirstNames.Any())
                return unisexFirstNames.RandomElement();

            return null;
        }

        public static string GetRandomLastName()
        {
            if (lastNames == null || !lastNames.Any())
                return null;

            return lastNames.RandomElement();
        }

        public static string GetRandomNickName(Gender gender)
        {
            if (maleNickNames == null || femaleNickNames == null || unisexNickNames == null)
                return null;

            if (gender == Gender.Male && maleNickNames.Any())
                return maleNickNames.RandomElement();
            else if (gender == Gender.Female && femaleNickNames.Any())
                return femaleNickNames.RandomElement();
            else if (unisexNickNames.Any())
                return unisexNickNames.RandomElement();

            return null;
        }
    }

    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "GeneratePawnName")]
    public static class PawnBioAndNameGenerator_GeneratePawnName_Patch
    {
        static void Postfix(ref Name __result, Pawn pawn, NameStyle style, string forcedLastName)
        {
            if (!AnimeArsenalSettings.enableDemonSlayerNames)
                return;

            if (pawn == null || __result == null)
                return;

            if (!pawn.RaceProps.Humanlike)
                return;

            if (style != NameStyle.Full)
                return;

            if (!Rand.Chance(AnimeArsenalSettings.demonSlayerNameChance))
                return;

            try
            {
                string firstName = DemonSlayerNameInjector.GetRandomFirstName(pawn.gender);
                string lastName = string.IsNullOrEmpty(forcedLastName) ?
                    DemonSlayerNameInjector.GetRandomLastName() : forcedLastName;
                string nickName = DemonSlayerNameInjector.GetRandomNickName(pawn.gender);
                
                if (!string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(lastName))
                {
                    __result = new NameTriple(firstName, nickName ?? firstName, lastName);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AnimeArsenal] Failed to generate Demon Slayer name: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(PermadeathModeUtility), "GeneratePermadeathSaveName")]
    public static class PermadeathSaveName_CrashFix
    {
        static Exception Finalizer(Exception __exception, ref string __result)
        {
            if (__exception != null)
            {
                __result = GenerateFallbackName();
                return null;
            }
            return null;
        }

        private static string GenerateFallbackName()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return $"Colony_{timestamp}";
        }
    }

    public class AnimeArsenalSettings : ModSettings
    {
        public static float demonSlayerNameChance = 0.25f;
        public static bool enableDemonSlayerNames = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref demonSlayerNameChance, "demonSlayerNameChance", 0.25f);
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

            listingStandard.CheckboxLabeled("Enable Demon Slayer Names", ref AnimeArsenalSettings.enableDemonSlayerNames,
                "When enabled, pawns will randomly receive Demon Slayer character names based on the frequency setting below.");

            listingStandard.Gap();

            listingStandard.Label($"Name Frequency: {AnimeArsenalSettings.demonSlayerNameChance:P0}");
            AnimeArsenalSettings.demonSlayerNameChance = listingStandard.Slider(AnimeArsenalSettings.demonSlayerNameChance, 0f, 1f);

            listingStandard.Gap(6f);

            Text.Font = GameFont.Tiny;
            listingStandard.Label("0% = No Demon Slayer names, 100% = Always Demon Slayer names");
            Text.Font = GameFont.Small;

            listingStandard.End();
            base.WriteSettings();
        }
    }
}