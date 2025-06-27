using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompAbilityEffect_CorpseMarionette : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (parent.def.defName == "CorpseMarionetteAbilityAOE")
            {
                AnimateCorpsesInRadius(target.Cell, parent.pawn.Map, 8.9f);
            }
            else
            {
                AnimateSingleCorpse(target, parent.pawn.Map);
            }
        }

        private void AnimateSingleCorpse(LocalTargetInfo target, Map map)
        {
            Corpse corpse = null;

            if (target.HasThing && target.Thing is Corpse)
            {
                corpse = target.Thing as Corpse;
            }
            else
            {
                corpse = target.Cell.GetFirstThing<Corpse>(map);
            }

            if (corpse != null)
            {
                AnimateCorpse(corpse, map);
            }
        }

        private void AnimateCorpsesInRadius(IntVec3 center, Map map, float radius)
        {
            IEnumerable<Thing> corpses = GenRadial.RadialDistinctThingsAround(center, map, radius, true)
                .Where(t => t is Corpse);

            foreach (Corpse corpse in corpses.Cast<Corpse>())
            {
                AnimateCorpse(corpse, map);
            }
        }

        private void AnimateCorpse(Corpse corpse, Map map)
        {
            if (corpse?.InnerPawn == null) return;

            Pawn innerPawn = corpse.InnerPawn;

            if (innerPawn.health.hediffSet.GetNotMissingParts().Count() < 3)
            {
                Messages.Message("Corpse too damaged to animate.", MessageTypeDefOf.RejectInput);
                return;
            }

            IntVec3 position = corpse.Position;
            corpse.Destroy();

            if (innerPawn.Dead)
            {
                ResurrectionUtility.TryResurrect(innerPawn);
            }

            GenSpawn.Spawn(innerPawn, position, map);

            if (parent.pawn.Faction != null)
            {
                innerPawn.SetFaction(parent.pawn.Faction);
            }

            HediffDef animationHediffDef = DefDatabase<HediffDef>.GetNamed("CorpseMarionetteAnimation");
            Hediff animationHediff = HediffMaker.MakeHediff(animationHediffDef, innerPawn);
            innerPawn.health.AddHediff(animationHediff);

            innerPawn.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent);

            ThoughtDef witnessThought = DefDatabase<ThoughtDef>.GetNamed("CorpseMarionetteWitnessed");
            foreach (Pawn witness in map.mapPawns.AllPawnsSpawned.Where(p =>
                p.RaceProps.Humanlike &&
                p.Position.DistanceTo(position) < 20f &&
                GenSight.LineOfSight(p.Position, position, map)))
            {
                witness.needs.mood.thoughts.memories.TryGainMemory(witnessThought);
            }

            FleckMaker.AttachedOverlay(innerPawn, FleckDefOf.PsycastSkipFlashEntry, Vector3.zero, 1f);

            Find.World.GetComponent<WorldComponent_CorpseMarionette>()?.RegisterAnimatedCorpse(innerPawn);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (parent.def.defName == "CorpseMarionetteAbilityAOE")
            {
                return target.IsValid;
            }
            else
            {
                Corpse corpse = target.Thing as Corpse ?? target.Cell.GetFirstThing<Corpse>(parent.pawn.Map);
                if (corpse == null)
                {
                    if (throwMessages) Messages.Message("No corpse found at target location.", MessageTypeDefOf.RejectInput);
                    return false;
                }
                return corpse.InnerPawn != null;
            }
        }
    }

    public class WorldComponent_CorpseMarionette : WorldComponent
    {
        private List<Pawn> animatedCorpses = new List<Pawn>();

        public WorldComponent_CorpseMarionette(World world) : base(world) { }

        public void RegisterAnimatedCorpse(Pawn pawn)
        {
            if (!animatedCorpses.Contains(pawn))
            {
                animatedCorpses.Add(pawn);
            }
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            if (Find.TickManager.TicksGame % 60 == 0)
            {
                for (int i = animatedCorpses.Count - 1; i >= 0; i--)
                {
                    Pawn pawn = animatedCorpses[i];
                    HediffDef animationHediffDef = DefDatabase<HediffDef>.GetNamed("CorpseMarionetteAnimation");
                    if (pawn?.health?.hediffSet?.GetFirstHediffOfDef(animationHediffDef) == null)
                    {
                        if (pawn != null && !pawn.Dead)
                        {
                            pawn.Kill(null);
                        }
                        animatedCorpses.RemoveAt(i);
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref animatedCorpses, "animatedCorpses", LookMode.Reference);
        }
    }
}