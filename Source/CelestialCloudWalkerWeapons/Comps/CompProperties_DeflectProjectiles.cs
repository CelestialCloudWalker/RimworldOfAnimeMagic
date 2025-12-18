using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_DeflectProjectiles : CompProperties_AbilityEffect
    {
        public float radius = 5f;
        public int TicksActive = 1250;
        public int MaxShotsToReflect = 10;
        public EffecterDef effecterDef;
        public HediffDef hediffDef;

        public CompProperties_DeflectProjectiles()
        {
            compClass = typeof(CompAbilityEffect_DeflectProjectiles);
        }
    }

    public class CompAbilityEffect_DeflectProjectiles : CompAbilityEffect
    {
        private new CompProperties_DeflectProjectiles Props => (CompProperties_DeflectProjectiles)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (parent?.pawn == null || Props == null) return;

            var hediffDef = Props.hediffDef ?? DeflectionHediffDefOf.AnimeArsenal_DeflectionField;
            if (hediffDef == null) return;

            
            var existing = parent.pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (existing != null)
                parent.pawn.health.RemoveHediff(existing);

            var hediff = (Hediff_DeflectionField)HediffMaker.MakeHediff(hediffDef, parent.pawn);
            hediff.Setup(Props.radius, Props.TicksActive, Props.MaxShotsToReflect, Props.effecterDef);
            parent.pawn.health.AddHediff(hediff);
        }
    }

    public class Hediff_DeflectionField : HediffWithComps
    {
        private float radius = 5f;
        private int ticksLeft;
        private int maxShots = 10;
        private int shotCount;
        private EffecterDef effecterDef;
        private Effecter effecter;
        private bool ready;

        public void Setup(float r, int ticks, int shots, EffecterDef eff)
        {
            radius = r;
            ticksLeft = ticks;
            maxShots = shots;
            shotCount = 0;
            effecterDef = eff;
            ready = true;

            if (effecterDef != null && pawn?.MapHeld != null)
            {
                effecter = effecterDef.Spawn(pawn, pawn.MapHeld);
                effecter?.Trigger(pawn, pawn);
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (!ready || pawn?.MapHeld == null)
            {
                pawn?.health?.RemoveHediff(this);
                return;
            }

            effecter?.EffectTick(pawn, pawn);

            if (ticksLeft <= 0 || shotCount >= maxShots)
            {
                pawn.health.RemoveHediff(this);
                return;
            }

            ticksLeft--;
            CheckForProjectiles();
        }

        private void CheckForProjectiles()
        {
            var nearby = GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.MapHeld, radius, true);

            foreach (var thing in nearby)
            {
                if (shotCount >= maxShots) break;

                if (thing is Projectile proj && proj.Launcher?.Faction != Faction.OfPlayer)
                {
                    ProjectileUtility.ReflectProjectile(proj, pawn);
                    shotCount++;
                }
            }
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            effecter?.Cleanup();
            effecter = null;
        }

        public override string LabelInBrackets
        {
            get
            {
                if (!ready) return base.LabelInBrackets;
                int seconds = Mathf.CeilToInt(ticksLeft / 60f);
                return $"{shotCount}/{maxShots} shots, {seconds}s";
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref radius, "radius", 5f);
            Scribe_Values.Look(ref ticksLeft, "ticksLeft");
            Scribe_Values.Look(ref maxShots, "maxShots", 10);
            Scribe_Values.Look(ref shotCount, "shotCount");
            Scribe_Defs.Look(ref effecterDef, "effecterDef");
            Scribe_Values.Look(ref ready, "ready");

            if (Scribe.mode == LoadSaveMode.PostLoadInit && ready && effecterDef != null && pawn?.MapHeld != null)
            {
                effecter = effecterDef.Spawn(pawn, pawn.MapHeld);
                effecter?.Trigger(pawn, pawn);
            }
        }
    }

    [DefOf]
    public static class DeflectionHediffDefOf
    {
        public static HediffDef AnimeArsenal_DeflectionField;
    }
}