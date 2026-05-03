using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AnimeArsenal
{
    public class IncidentWorker_RaidNightOnly : IncidentWorker_RaidEnemy
    {
        private const string DemonFactionDef = "AA_Twelve_Demon_Moons";
        private const float NightGlowThreshold = 0.3f;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
                return false;

            if (parms.faction == null || parms.faction.def.defName != DemonFactionDef)
                return true;

            Map map = parms.target as Map;
            if (map?.skyManager == null)
                return false;

            return map.skyManager.CurSkyGlow < NightGlowThreshold;
        }
    }

    public class LordToil_AssaultColonyCannibal : LordToil_AssaultColony
    {
        private const float HungerThreshold = 0.8f;
        private const float SearchRadius = 20f;
        private const int CheckInterval = 60;

        private int lastCheckTick = -999;

        public override void LordToilTick()
        {
            base.LordToilTick();

            if (Find.TickManager.TicksGame - lastCheckTick < CheckInterval)
                return;

            lastCheckTick = Find.TickManager.TicksGame;

            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn demon = lord.ownedPawns[i];
                if (demon == null || demon.Dead || demon.Downed)
                    continue;
                if (demon.mindState?.enemyTarget != null)
                    continue;
                if (demon.needs?.food != null && demon.needs.food.CurLevelPercentage < HungerThreshold)
                    TryMakeDemonEat(demon);
            }
        }

        private void TryMakeDemonEat(Pawn demon)
        {
            if (demon?.Map == null || demon.CurJobDef == JobDefOf.Ingest)
                return;

            Thing food = FindNearestFood(demon);
            if (food == null)
                return;

            Job job = JobMaker.MakeJob(JobDefOf.Ingest, food);
            job.count = 1;
            job.playerForced = true;
            demon.jobs?.StopAll();
            demon.jobs?.StartJob(job, JobCondition.InterruptForced);
        }

        private Thing FindNearestFood(Pawn demon)
        {
            if (demon?.Map == null) return null;

            float maxDistSq = SearchRadius * SearchRadius;
            Thing best = null;
            float bestDistSq = maxDistSq;

            foreach (Pawn pawn in demon.Map.mapPawns.AllPawnsSpawned)
            {
                if (!pawn.Downed || pawn.Dead || pawn.Faction == demon.Faction)
                    continue;
                if (!pawn.RaceProps.Humanlike)
                    continue;
                if (!FoodUtility.WillEat(demon, pawn))
                    continue;
                if (!demon.CanReserveAndReach(pawn, PathEndMode.Touch, Danger.Deadly))
                    continue;

                float d = (pawn.Position - demon.Position).LengthHorizontalSquared;
                if (d < bestDistSq) { bestDistSq = d; best = pawn; }
            }

            if (best != null) return best;

            foreach (Thing thing in demon.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                Corpse corpse = thing as Corpse;
                if (corpse == null || !corpse.InnerPawn.RaceProps.Humanlike || corpse.IsDessicated())
                    continue;
                if (!FoodUtility.WillEat(demon, corpse))
                    continue;
                if (!demon.CanReserveAndReach(corpse, PathEndMode.Touch, Danger.Deadly))
                    continue;

                float d = (corpse.Position - demon.Position).LengthHorizontalSquared;
                if (d < bestDistSq) { bestDistSq = d; best = corpse; }
            }

            return best;
        }
    }
}