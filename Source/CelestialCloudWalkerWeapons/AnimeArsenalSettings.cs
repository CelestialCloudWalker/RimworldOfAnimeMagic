using RimWorld;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class AnimeArsenalSettings : ModSettings
    {
        public static bool enableDemonSlayerNames = true;
        public static float demonSlayerNameChance = 0.25f;
        public static bool demonRaidsEnabled = true;
        public static float minDaysBetweenDemonRaids = 3f;
        public static float demonRaidDifficultyMult = 1.5f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableDemonSlayerNames, "enableDemonSlayerNames", true);
            Scribe_Values.Look(ref demonSlayerNameChance, "demonSlayerNameChance", 0.25f);
            Scribe_Values.Look(ref demonRaidsEnabled, "demonRaidsEnabled", true);
            Scribe_Values.Look(ref minDaysBetweenDemonRaids, "minDaysBetweenDemonRaids", 3f);
            Scribe_Values.Look(ref demonRaidDifficultyMult, "demonRaidDifficultyMult", 1.5f);
        }
    }

    public class AnimeArsenalMod : Mod
    {
        public AnimeArsenalMod(ModContentPack content) : base(content)
        {
            GetSettings<AnimeArsenalSettings>();
        }

        public override string SettingsCategory() => "Rimketsu";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            list.Label("Pawn Names");
            list.GapLine(6f);

            list.CheckboxLabeled(
                "Enable Demon Slayer names",
                ref AnimeArsenalSettings.enableDemonSlayerNames,
                "Newly-generated pawns have a chance to receive Demon Slayer character names.");

            if (AnimeArsenalSettings.enableDemonSlayerNames)
            {
                list.Gap(4f);
                list.Label($"Name frequency:  {AnimeArsenalSettings.demonSlayerNameChance:P0}",
                    tooltip: "0 % = never, 100 % = every pawn.");
                AnimeArsenalSettings.demonSlayerNameChance =
                    list.Slider(AnimeArsenalSettings.demonSlayerNameChance, 0f, 1f);

                Text.Font = GameFont.Tiny;
                list.Label("   0 % = No Demon Slayer names       100 % = Always Demon Slayer names");
                Text.Font = GameFont.Small;
            }

            list.Gap(16f);

            list.Label("Demon Raids");
            list.GapLine(6f);

            list.CheckboxLabeled(
                "Enable night demon raids",
                ref AnimeArsenalSettings.demonRaidsEnabled,
                "Toggles whether the Twelve Demon Moons raid your colony at night.");

            if (AnimeArsenalSettings.demonRaidsEnabled)
            {
                list.Gap(4f);
                list.Label(
                    $"Minimum days between raids:  {AnimeArsenalSettings.minDaysBetweenDemonRaids:F1}",
                    tooltip: "Lower = more frequent. This is a minimum cooldown between nightly raid attempts.");
                AnimeArsenalSettings.minDaysBetweenDemonRaids =
                    list.Slider(AnimeArsenalSettings.minDaysBetweenDemonRaids, 1f, 30f);

                float d = AnimeArsenalSettings.minDaysBetweenDemonRaids;
                string freqHint = d <= 2f ? "Very frequent — expect near-nightly raids." :
                                  d <= 5f ? "Frequent — raids every few nights." :
                                  d <= 10f ? "Moderate — occasional demon visits." :
                                  d <= 20f ? "Rare — demons show up infrequently." :
                                             "Very rare — mostly peaceful nights.";
                Text.Font = GameFont.Tiny;
                list.Label($"   ({freqHint})");
                Text.Font = GameFont.Small;

                list.Gap(8f);
                list.Label(
                    $"Raid difficulty multiplier:  {AnimeArsenalSettings.demonRaidDifficultyMult:F2}x",
                    tooltip: "Multiplied on top of your normal threat points. " +
                             "1.0 = same as a regular raid, 2.0 = double the threat budget.");
                AnimeArsenalSettings.demonRaidDifficultyMult =
                    list.Slider(AnimeArsenalSettings.demonRaidDifficultyMult, 1f, 4f);

                float m = AnimeArsenalSettings.demonRaidDifficultyMult;
                string diffHint = m < 1.25f ? "Normal — same strength as regular raids." :
                                  m < 1.75f ? "Tougher — noticeably stronger than normal." :
                                  m < 2.5f ? "Hard — significantly above your threat level." :
                                  m < 3.5f ? "Brutal — demons are far stronger than you." :
                                              "Nightmare — good luck.";
                Text.Font = GameFont.Tiny;
                list.Label($"   ({diffHint})");
                Text.Font = GameFont.Small;
            }

            list.End();
            base.WriteSettings();
        }
    }
}