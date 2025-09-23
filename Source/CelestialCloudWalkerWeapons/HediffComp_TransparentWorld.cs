using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class TransparentWorldProperties : DefModExtension
    {
        public float sightRange = 15f;
        public int durationTicks = 3600;
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
                catch
                {
                    
                }
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
            if (Props.hediffDef == null) return;

            Pawn targetPawn = this.parent.pawn;
            if (targetPawn == null) return;

            var existingHediffs = targetPawn.health.hediffSet.hediffs
                .Where(h => h.def.defName.StartsWith("TransparentWorld_"))
                .ToList();

            foreach (var hediff in existingHediffs)
            {
                targetPawn.health.RemoveHediff(hediff);
            }

            Hediff newHediff = HediffMaker.MakeHediff(Props.hediffDef, targetPawn);
            targetPawn.health.AddHediff(newHediff);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return this.parent.pawn != null;
        }
    }
}