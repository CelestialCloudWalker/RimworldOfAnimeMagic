using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AnimeArsenal
{
    public class HediffCompProperties_StringPerformance : HediffCompProperties
    {
        public int pulseIntervalTicks = 30;
        public float radius = 12f;
        public DamageDef damageType;
        public FloatRange damagePerPulse = new FloatRange(6f, 10f);
        public float armorPen = 0.2f;
        public StatDef scaleStat;
        public SkillDef scaleSkill;
        public float skillMultiplier = 0.1f;
        public bool debugScaling = false;
        public bool spawnADB = false;
        public ThingDef adbProjectileDef;
        public EffecterDef pulseEffecter;
        public EffecterDef castEffecter;
        public bool usePulseEffecter = true;

        public HediffCompProperties_StringPerformance()
        {
            compClass = typeof(HediffComp_StringPerformance);
        }
    }

    public class HediffComp_StringPerformance : HediffComp
    {
        public HediffCompProperties_StringPerformance Props =>
            (HediffCompProperties_StringPerformance)props;

        private int tickCounter = 0;
        private bool castEffecterPlayed = false;

        public override void CompPostMake()
        {
            base.CompPostMake();
            if (Props.castEffecter != null && !castEffecterPlayed && Pawn?.Map != null)
            {
                Effecter e = Props.castEffecter.Spawn();
                e.Trigger(new TargetInfo(Pawn.Position, Pawn.Map), new TargetInfo(Pawn.Position, Pawn.Map));
                e.Cleanup();
                castEffecterPlayed = true;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            tickCounter++;
            if (tickCounter >= Props.pulseIntervalTicks)
            {
                tickCounter = 0;
                DoPulse();
            }
        }

        private void DoPulse()
        {
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || pawn.Map == null) return;

            Map map = pawn.Map;

            List<Pawn> targets = new List<Pawn>();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(
                pawn.Position, map, Props.radius, true))
            {
                if (thing is Pawn p && p != pawn && p.HostileTo(pawn))
                    targets.Add(p);
            }

            foreach (Pawn target in targets)
            {
                if (target.Dead) continue;

                float scaled = DamageScalingUtility.GetScaledDamage(
                    Props.damagePerPulse.RandomInRange,
                    pawn, Props.scaleStat, Props.scaleSkill,
                    Props.skillMultiplier, Props.debugScaling);

                DamageInfo dinfo = new DamageInfo(
                    Props.damageType ?? DamageDefOf.Cut,
                    scaled, Props.armorPen, -1f, pawn);
                target.TakeDamage(dinfo);

                if (Props.pulseEffecter != null && Props.usePulseEffecter)
                {
                    Effecter e = Props.pulseEffecter.Spawn();
                    e.Trigger(new TargetInfo(target.Position, map), new TargetInfo(target.Position, map));
                    e.Cleanup();
                }

                if (Props.spawnADB && Props.adbProjectileDef != null)
                    SpawnADBAt(target.Position, pawn, map);
            }
        }

        private void SpawnADBAt(IntVec3 cell, Pawn instigator, Map map)
        {
            Thing thing = ThingMaker.MakeThing(Props.adbProjectileDef);
            if (thing is Projectile proj)
            {
                GenSpawn.Spawn(proj, instigator.Position, map);
                proj.Launch(instigator, cell, cell, ProjectileHitFlags.All);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref tickCounter, "stringPerfTickCounter", 0);
            Scribe_Values.Look(ref castEffecterPlayed, "castEffecterPlayed", false);
        }
    }

    public class CompProperties_StringPerformance : CompProperties_AbilityEffect
    {
        public HediffDef hediffDef;
        public EffecterDef castEffecter;

        public CompProperties_StringPerformance()
        {
            compClass = typeof(CompAbilityEffect_StringPerformance);
        }
    }

    public class CompAbilityEffect_StringPerformance : CompAbilityEffect
    {
        public new CompProperties_StringPerformance Props =>
            (CompProperties_StringPerformance)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster == null || Props.hediffDef == null) return;

            if (Props.castEffecter != null && caster.Map != null)
            {
                Effecter e = Props.castEffecter.Spawn();
                e.Trigger(new TargetInfo(caster.Position, caster.Map), new TargetInfo(caster.Position, caster.Map));
                e.Cleanup();
            }

            Hediff existing = caster.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
            if (existing != null)
                caster.health.RemoveHediff(existing);

            Hediff hediff = HediffMaker.MakeHediff(Props.hediffDef, caster);
            caster.health.AddHediff(hediff);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return parent?.pawn != null;
        }
    }
}