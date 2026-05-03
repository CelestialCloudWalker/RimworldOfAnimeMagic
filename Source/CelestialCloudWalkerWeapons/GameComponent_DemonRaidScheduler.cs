using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class GameComponent_DemonRaidScheduler : GameComponent
    {
        private const string DemonFactionDefName = "AA_Twelve_Demon_Moons";
        private const string IncidentDefName     = "NightDemonRaid";
        private const float  NightGlowThreshold  = 0.3f;
        private const float  MinPoints           = 100f;

        private Dictionary<int, int>  lastFireTickByMap = new Dictionary<int, int>();
        private Dictionary<int, bool> wasNightByMap     = new Dictionary<int, bool>();

        private int CooldownTicks =>
            (int)(AnimeArsenalSettings.minDaysBetweenDemonRaids * GenDate.TicksPerDay);

        public GameComponent_DemonRaidScheduler(Game game) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref lastFireTickByMap, "lastFireTickByMap",
                LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref wasNightByMap, "wasNightByMap",
                LookMode.Value, LookMode.Value);

            if (lastFireTickByMap == null) lastFireTickByMap = new Dictionary<int, int>();
            if (wasNightByMap == null)     wasNightByMap     = new Dictionary<int, bool>();
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 300 != 0)
                return;

            if (!AnimeArsenalSettings.demonRaidsEnabled)
                return;

            foreach (Map map in Find.Maps)
            {
                if (!map.IsPlayerHome || map.skyManager == null) continue;

                float glow    = map.skyManager.CurSkyGlow;
                bool  isNight = glow < NightGlowThreshold;
                int   mapID   = map.uniqueID;

                wasNightByMap.TryGetValue(mapID, out bool wasNight);

                if (isNight && !wasNight)
                {
                    Log.Message($"[AA Demon Raid] Night fell on map {mapID} (glow={glow:F2}) — attempting raid");
                    TryFireRaid(map);
                }

                wasNightByMap[mapID] = isNight;
            }
        }

        private void TryFireRaid(Map map)
        {
            int mapID = map.uniqueID;

            if (lastFireTickByMap.TryGetValue(mapID, out int lastTick))
            {
                int   ticksSince = Find.TickManager.TicksGame - lastTick;
                float daysLeft   = (CooldownTicks - ticksSince) / (float)GenDate.TicksPerDay;
                if (ticksSince < CooldownTicks)
                {
                    Log.Message($"[AA Demon Raid] Cooldown — {daysLeft:F1} days remaining, skipping");
                    return;
                }
            }

            FactionDef fDef = DefDatabase<FactionDef>.GetNamedSilentFail(DemonFactionDefName);
            if (fDef == null)
            {
                Log.Warning("[AA Demon Raid] FactionDef AA_Twelve_Demon_Moons not found — check your XML");
                return;
            }
            Faction faction = Find.FactionManager.FirstFactionOfDef(fDef);
            if (faction == null)
            {
                Log.Warning("[AA Demon Raid] Faction not present in this world");
                return;
            }

            IncidentDef incDef = DefDatabase<IncidentDef>.GetNamedSilentFail(IncidentDefName);
            if (incDef == null)
            {
                Log.Warning("[AA Demon Raid] IncidentDef NightDemonRaid not found — check your XML");
                return;
            }

            IncidentParms parms = new IncidentParms();
            parms.target  = map;
            parms.faction = faction;
            parms.points  = Mathf.Max(
                StorytellerUtility.DefaultThreatPointsNow(map) * AnimeArsenalSettings.demonRaidDifficultyMult,
                MinPoints
            );

            Log.Message($"[AA Demon Raid] Firing — faction: {faction.Name}, points: {parms.points:F0}");

            bool success = incDef.Worker.TryExecute(parms);

            Log.Message($"[AA Demon Raid] TryExecute result: {success}");

            if (success)
                lastFireTickByMap[mapID] = Find.TickManager.TicksGame;
        }
    }
}