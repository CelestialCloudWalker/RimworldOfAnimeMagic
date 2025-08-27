using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_DeflectProjectiles : CompProperties_AbilityEffect
    {
        public float Radius = 5f;
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
            Log.Message($"=== DeflectProjectiles Apply called ===");
            Log.Message($"Parent: {parent?.def?.defName}");
            Log.Message($"Props null: {Props == null}");
            if (Props != null)
            {
                Log.Message($"Props values - Radius: {Props.Radius}, TicksActive: {Props.TicksActive}, MaxShots: {Props.MaxShotsToReflect}");
            }

            base.Apply(target, dest);

            if (Props == null)
            {
                Log.Error($"CompAbilityEffect_DeflectProjectiles: Props is null for ability {parent?.def?.defName}");
                return;
            }

            ApplyDeflectionHediff();
        }

        private void ApplyDeflectionHediff()
        {
            if (parent?.pawn == null)
                return;

            HediffDef hediffToApply = Props.hediffDef ?? DeflectionHediffDefOf.AnimeArsenal_DeflectionField;

            if (hediffToApply == null)
            {
                Log.Error("No deflection hediff def found!");
                return;
            }

            var existingHediff = parent.pawn.health.hediffSet.GetFirstHediffOfDef(hediffToApply);
            if (existingHediff != null)
            {
                parent.pawn.health.RemoveHediff(existingHediff);
            }

            var hediff = (Hediff_DeflectionField)HediffMaker.MakeHediff(hediffToApply, parent.pawn);
            hediff.Initialize(Props.Radius, Props.TicksActive, Props.MaxShotsToReflect, Props.effecterDef);

            parent.pawn.health.AddHediff(hediff);

            Log.Message($"Applied deflection hediff with Radius: {Props.Radius}, Duration: {Props.TicksActive}, MaxShots: {Props.MaxShotsToReflect}");
        }
    }

    public class Hediff_DeflectionField : HediffWithComps
    {
        private float radius;
        private int ticksRemaining;
        private int maxShotsToReflect;
        private int shotsReflected;
        private EffecterDef effecterDef;
        private Effecter effector;
        private bool initialized = false;

        public void Initialize(float fieldRadius, int duration, int maxShots, EffecterDef effecter)
        {
            radius = fieldRadius;
            ticksRemaining = duration;
            maxShotsToReflect = maxShots;
            shotsReflected = 0;
            effecterDef = effecter;
            initialized = true;

            Log.Message($"DeflectionField hediff initialized - Radius: {radius}, Duration: {duration}, MaxShots: {maxShots}");

            if (effecterDef != null && pawn?.MapHeld != null)
            {
                try
                {
                    effector = effecterDef.Spawn(pawn, pawn.MapHeld);
                    effector?.Trigger(pawn, pawn);
                }
                catch (System.Exception e)
                {
                    Log.Error($"Failed to create deflection effector: {e}");
                }
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (!initialized || pawn == null || pawn.Destroyed)
            {
                pawn?.health?.RemoveHediff(this);
                return;
            }

            Log.Message($"DeflectionField Tick - TicksRemaining: {ticksRemaining}, ShotsReflected: {shotsReflected}/{maxShotsToReflect}");

            if (effector != null)
            {
                effector.EffectTick(pawn, pawn);
            }

            if (ticksRemaining <= 0 || shotsReflected >= maxShotsToReflect)
            {
                Log.Message($"DeflectionField ending - TicksRemaining: {ticksRemaining}, Shots: {shotsReflected}/{maxShotsToReflect}");
                pawn.health.RemoveHediff(this);
                return;
            }

            ticksRemaining--;
            DeflectProjectiles();
        }

        private void DeflectProjectiles()
        {
            if (pawn?.MapHeld == null)
                return;

            try
            {
                var projectilesInRange = GenRadial.RadialDistinctThingsAround(
                    pawn.Position,
                    pawn.MapHeld,
                    radius,
                    true);

                int projectilesThisTick = 0;
                foreach (var item in projectilesInRange)
                {
                    if (shotsReflected >= maxShotsToReflect)
                        break;

                    if (item is Projectile projectile &&
                        projectile.Launcher?.Faction != Faction.OfPlayer)
                    {
                        ProjectileUtility.ReflectProjectile(projectile, pawn);
                        shotsReflected++;
                        projectilesThisTick++;
                        Log.Message($"Reflected projectile #{shotsReflected} - {projectile.def.defName}");
                    }
                }

                if (projectilesThisTick > 0)
                {
                    Log.Message($"Reflected {projectilesThisTick} projectiles this tick, total: {shotsReflected}/{maxShotsToReflect}");
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"Error in DeflectProjectiles: {e}");
                pawn?.health?.RemoveHediff(this);
            }
        }

        public override void PostRemoved()
        {
            base.PostRemoved();

            if (effector != null)
            {
                try
                {
                    effector.Cleanup();
                }
                catch (System.Exception e)
                {
                    Log.Warning($"Error cleaning up deflection effector: {e}");
                }
                effector = null;
            }

            Log.Message($"DeflectionField hediff removed - reflected {shotsReflected} total shots");
        }

        public override string LabelInBrackets
        {
            get
            {
                if (!initialized)
                    return base.LabelInBrackets;

                int secondsRemaining = Mathf.CeilToInt(ticksRemaining / 60f);
                return $"{shotsReflected}/{maxShotsToReflect} shots, {secondsRemaining}s";
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref radius, "radius", 5f);
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
            Scribe_Values.Look(ref maxShotsToReflect, "maxShotsToReflect", 10);
            Scribe_Values.Look(ref shotsReflected, "shotsReflected", 0);
            Scribe_Defs.Look(ref effecterDef, "effecterDef");
            Scribe_Values.Look(ref initialized, "initialized", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && initialized && effecterDef != null && pawn?.MapHeld != null)
            {
                try
                {
                    effector = effecterDef.Spawn(pawn, pawn.MapHeld);
                    effector?.Trigger(pawn, pawn);
                }
                catch (System.Exception e)
                {
                    Log.Warning($"Failed to recreate deflection effector after loading: {e}");
                }
            }
        }
    }

    [DefOf]
    public static class DeflectionHediffDefOf
    {
        public static HediffDef AnimeArsenal_DeflectionField;
    }
}