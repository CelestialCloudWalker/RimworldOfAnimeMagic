using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AnimeArsenal
{
    public class IncidentWorker_RaidNightOnly : IncidentWorker_RaidEnemy
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
                return false;

            if (parms.faction == null || parms.faction.def.defName != "AA_Twelve_Demon_Moons")
                return base.CanFireNowSub(parms);

            Map map = (Map)parms.target;
            if (map == null || map.skyManager == null)
                return false;
            return map.skyManager.CurSkyGlow < 0.3f;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (parms.faction != null && parms.faction.def.defName == "AA_Twelve_Demon_Moons")
            {
                Map map = (Map)parms.target;
                if (map == null || map.skyManager == null)
                {
                    Log.Warning("[AnimeArsenal] Demon raid failed - no sky manager found");
                    return false;
                }

                float skyGlow = map.skyManager.CurSkyGlow;
                if (skyGlow >= 0.3f)
                {
                    return false;
                }
            }

            bool success = base.TryExecuteWorker(parms);

            if (success && parms.faction != null && parms.faction.def.defName == "AA_Twelve_Demon_Moons")
            {
                Map map = (Map)parms.target;

                Lord raidLord = null;
                for (int i = map.lordManager.lords.Count - 1; i >= 0; i--)
                {
                    if (map.lordManager.lords[i].faction == parms.faction)
                    {
                        raidLord = map.lordManager.lords[i];
                        break;
                    }
                }

                if (raidLord != null)
                {
                    LordJob_AssaultColonyNightRaid nightJob = new LordJob_AssaultColonyNightRaid(
                        parms.faction, true, true, false, false, true);
                    raidLord.SetJob(nightJob);
                }
            }

            return success;
        }
    }

    public class LordJob_AssaultColonyNightRaid : LordJob_AssaultColony
    {
        private int lightCheckTick = -1;
        private bool retreatedAlready = false;

        public LordJob_AssaultColonyNightRaid() { }

        public LordJob_AssaultColonyNightRaid(Faction faction, bool canKidnap, bool canTimeoutOrFlee,
            bool sappers, bool useAvoidGridSmart, bool canSteal)
            : base(faction, canKidnap, canTimeoutOrFlee, sappers, useAvoidGridSmart, canSteal) { }

        public override void LordJobTick()
        {
            base.LordJobTick();

            if (retreatedAlready)
                return;

            if (Find.TickManager.TicksGame - lightCheckTick > 60)
            {
                lightCheckTick = Find.TickManager.TicksGame;

                if (lord != null && lord.Map != null)
                {
                    float glow = lord.Map.skyManager.CurSkyGlow;
                    if (glow > 0.5f)
                    {
                        retreatedAlready = true;
                        lord.ReceiveMemo("SunUp");
                    }
                }
            }
        }

        public override StateGraph CreateGraph()
        {
            StateGraph graph = base.CreateGraph();

            LordToil_SunriseRetreat retreatToil = new LordToil_SunriseRetreat();
            graph.AddToil(retreatToil);

            foreach (LordToil toil in graph.lordToils)
            {
                if (toil == retreatToil) continue;

                Transition sunUpTransition = new Transition(toil, retreatToil);
                sunUpTransition.AddTrigger(new Trigger_Memo("SunUp"));
                graph.AddTransition(sunUpTransition);
            }

            return graph;
        }
    }

    public class LordToil_SunriseRetreat : LordToil_ExitMap
    {
        public override void Init()
        {
            base.Init();
            Messages.Message("The demons retreat as dawn breaks!", MessageTypeDefOf.NeutralEvent);
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                lord.ownedPawns[i].mindState.duty = new PawnDuty(DutyDefOf.ExitMapBest);
            }
        }
    }
}