using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class TransparentWorldProperties : DefModExtension
    {
        public float sightRange = 15f;
        public int durationTicks = 3600;
        public float organHitChanceBonus = 0f;
        public float vitalOrganChanceMultiplier = 1f;
    }

    public class Hediff_TransparentWorld : Hediff
    {
        private int ticksRemaining;
        private bool initialized = false;
        private TransparentWorldProperties Properties => def.GetModExtension<TransparentWorldProperties>();

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (!initialized && Properties != null)
            {
                ticksRemaining = Properties.durationTicks;
                initialized = true;
            }
        }

        public override void Tick()
        {
            base.Tick();
            if (this.pawn == null || !this.pawn.Spawned || this.pawn.Map == null || Properties == null)
                return;

            ticksRemaining--;
            if (ticksRemaining <= 0)
            {
                this.pawn.health.RemoveHediff(this);
                return;
            }

            if (Find.TickManager.TicksGame % 60 == 0)
            {
                try
                {
                    Map map = this.pawn.Map;
                    float range = Properties.sightRange;
                    foreach (IntVec3 cell in map.AllCells.Where(x => x.DistanceTo(this.pawn.Position) <= range && x.Fogged(map)))
                    {
                        map.fogGrid.Unfog(cell);
                    }
                }
                catch { }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
            Scribe_Values.Look(ref initialized, "initialized", false);
        }

        public override string LabelInBrackets
        {
            get
            {
                int secondsRemaining = Mathf.CeilToInt(ticksRemaining / 60f);
                return secondsRemaining + "s";
            }
        }
    }

    public class CompProperties_GiveHediff : CompProperties_AbilityEffect
    {
        public HediffDef hediffDef;
        public float severityAdjustment = 0f;
        public bool applyToTarget = true;
        public float effectRadius = 0f;
        public List<HediffDef> hediffsToRemove = new List<HediffDef>();
        public HediffDef casterHediffDef;
        public float casterHediffSeverity = 1.0f;

        public CompProperties_GiveHediff()
        {
            compClass = typeof(CompAbilityEffect_GiveHediff);
        }
    }

    public class CompAbilityEffect_GiveHediff : CompAbilityEffect
    {
        public new CompProperties_GiveHediff Props => (CompProperties_GiveHediff)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (Props.casterHediffDef != null && parent.pawn != null && !parent.pawn.Dead)
            {
                Hediff casterHediff = HediffMaker.MakeHediff(Props.casterHediffDef, parent.pawn);
                casterHediff.Severity = Props.casterHediffSeverity;
                parent.pawn.health.AddHediff(casterHediff);
            }

            if (Props.hediffDef == null && Props.hediffsToRemove.NullOrEmpty()) return;

            if (Props.effectRadius > 0f)
            {
                foreach (Thing thing in GenRadial.RadialDistinctThingsAround(
                    target.Cell, parent.pawn.Map, Props.effectRadius, true))
                {
                    if (thing is Pawn pawn && !pawn.Dead && pawn != parent.pawn)
                    {
                        ApplyHediffToPawn(pawn);
                    }
                }
            }
            else
            {
                Pawn targetPawn = Props.applyToTarget
                    ? target.Thing as Pawn
                    : parent.pawn;

                if (targetPawn == null || targetPawn.Dead) return;
                ApplyHediffToPawn(targetPawn);
            }
        }

        private void ApplyHediffToPawn(Pawn targetPawn)
        {
            var existingTransparent = targetPawn.health.hediffSet.hediffs
                .Where(h => h.def.defName.StartsWith("TransparentWorld_"))
                .ToList();
            foreach (var hediff in existingTransparent)
                targetPawn.health.RemoveHediff(hediff);

            if (!Props.hediffsToRemove.NullOrEmpty())
            {
                foreach (HediffDef defToRemove in Props.hediffsToRemove)
                {
                    Hediff existing = targetPawn.health.hediffSet.GetFirstHediffOfDef(defToRemove);
                    if (existing != null)
                        targetPawn.health.RemoveHediff(existing);
                }
            }

            if (Props.hediffDef == null) return;

            if (Props.severityAdjustment > 0f)
            {
                Hediff existing = targetPawn.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
                if (existing != null)
                {
                    existing.Severity += Props.severityAdjustment;
                    return;
                }
            }

            Hediff newHediff = HediffMaker.MakeHediff(Props.hediffDef, targetPawn);
            if (Props.severityAdjustment > 0f)
                newHediff.Severity = Props.severityAdjustment;
            targetPawn.health.AddHediff(newHediff);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (Props.effectRadius > 0f) return true;
            if (Props.applyToTarget)
            {
                if (target.Thing is Pawn pawn && !pawn.Dead) return true;
                if (throwMessages)
                    Messages.Message("Must target a living pawn", MessageTypeDefOf.RejectInput);
                return false;
            }
            return parent.pawn != null;
        }
    }



    public class CompProperties_GiveHediff_Caster : CompProperties_GiveHediff
    {
        public CompProperties_GiveHediff_Caster()
        {
            compClass = typeof(CompAbilityEffect_GiveHediff_Caster);
        }
    }

    public class CompAbilityEffect_GiveHediff_Caster : CompAbilityEffect_GiveHediff { }

    public class CompProperties_GiveHediff_Target : CompProperties_GiveHediff
    {
        public CompProperties_GiveHediff_Target()
        {
            compClass = typeof(CompAbilityEffect_GiveHediff_Target);
        }
    }

    public class CompAbilityEffect_GiveHediff_Target : CompAbilityEffect_GiveHediff { }
}
