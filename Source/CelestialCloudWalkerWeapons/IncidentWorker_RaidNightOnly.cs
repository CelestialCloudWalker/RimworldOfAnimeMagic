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
                        parms.faction, false, true, false, false, true);
                   
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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lightCheckTick, "lightCheckTick", -1);
            Scribe_Values.Look(ref retreatedAlready, "retreatedAlready", false);
        }

        public override void LordJobTick()
        {
            base.LordJobTick();

            if (retreatedAlready)
                return;

            if (Find.TickManager.TicksGame - lightCheckTick > 60)
            {
                lightCheckTick = Find.TickManager.TicksGame;

                if (lord != null && lord.Map != null && lord.Map.skyManager != null)
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

            LordToil_AssaultColonyCannibal assaultToil = new LordToil_AssaultColonyCannibal();

            LordToil originalAssault = graph.lordToils.FirstOrDefault(t => t is LordToil_AssaultColony);
            if (originalAssault != null)
            {
                int index = graph.lordToils.IndexOf(originalAssault);
                graph.lordToils[index] = assaultToil;

                foreach (var transition in graph.transitions)
                {
                    if (transition.target == originalAssault)
                    {
                        transition.target = assaultToil;
                    }
                }
            }

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

    public class LordToil_AssaultColonyCannibal : LordToil_AssaultColony
    {
        private int checkTickInterval = 60;
        private int lastCheckTick = -999;

        public override void UpdateAllDuties()
        {
            base.UpdateAllDuties();
        }

        public override void LordToilTick()
        {
            base.LordToilTick();

            if (Find.TickManager.TicksGame - lastCheckTick < checkTickInterval)
                return;

            lastCheckTick = Find.TickManager.TicksGame;

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn demon = lord.ownedPawns[i];

                if (demon == null || demon.Dead || demon.Downed)
                    continue;

                if (demon.mindState?.enemyTarget != null)
                    continue;

                if (demon.needs?.food != null && demon.needs.food.CurLevelPercentage < 0.8f)
                {
                    TryMakeDemonEat(demon);
                }
            }
        }

        private void TryMakeDemonEat(Pawn demon)
        {
            if (demon?.Map == null || demon.CurJobDef == JobDefOf.Ingest)
                return;

            Thing foodSource = FindNearestFoodSource(demon);

            if (foodSource != null)
            {
                Job eatJob = JobMaker.MakeJob(JobDefOf.Ingest, foodSource);
                eatJob.count = 1;
                eatJob.playerForced = true; 

                if (demon.jobs != null)
                {
                    demon.jobs.StopAll();
                    demon.jobs.StartJob(eatJob, JobCondition.InterruptForced);
                }
            }
        }

        private Thing FindNearestFoodSource(Pawn demon)
        {
            if (demon?.Map == null) return null;

            float maxDist = 20f;
            Thing closestFood = null;
            float closestDistSq = maxDist * maxDist;

            foreach (Pawn pawn in demon.Map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Downed &&
                    !pawn.Dead &&
                    pawn.Faction != demon.Faction &&
                    pawn.RaceProps.Humanlike &&
                    FoodUtility.WillEat(demon, pawn) &&
                    demon.CanReserveAndReach(pawn, PathEndMode.Touch, Danger.Deadly))
                {
                    float distSq = (pawn.Position - demon.Position).LengthHorizontalSquared;
                    if (distSq < closestDistSq)
                    {
                        closestDistSq = distSq;
                        closestFood = pawn;
                    }
                }
            }

            if (closestFood == null)
            {
                List<Thing> corpses = demon.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
                foreach (Thing thing in corpses)
                {
                    Corpse corpse = thing as Corpse;
                    if (corpse != null &&
                        corpse.InnerPawn.RaceProps.Humanlike &&
                        !corpse.IsDessicated() &&
                        FoodUtility.WillEat(demon, corpse) &&
                        demon.CanReserveAndReach(corpse, PathEndMode.Touch, Danger.Deadly))
                    {
                        float distSq = (corpse.Position - demon.Position).LengthHorizontalSquared;
                        if (distSq < closestDistSq)
                        {
                            closestDistSq = distSq;
                            closestFood = corpse;
                        }
                    }
                }
            }

            return closestFood;
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