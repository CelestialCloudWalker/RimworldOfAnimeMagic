using RimWorld;
using System;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{
    public class SelflessStateProperties : DefModExtension
    {
        public int durationTicks = 1000;
        public int fadeDurationTicks = 75;
    }

    public class Hediff_SelflessState : HediffWithComps
    {
        private int ticksRemaining;
        private bool initialized = false;
        private int fadeTicksRemaining = 0;

        private SelflessStateProperties Properties => def.GetModExtension<SelflessStateProperties>();

        public bool IsInvisible => initialized && fadeTicksRemaining <= 0 && ticksRemaining > 0;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (!initialized && Properties != null)
            {
                ticksRemaining = Properties.durationTicks;
                fadeTicksRemaining = Properties.fadeDurationTicks;
                initialized = true;
            }
        }

        public override void Tick()
        {
            base.Tick();
            if (this.pawn == null || !this.pawn.Spawned)
                return;

            if (Properties != null && ticksRemaining > 0)
            {
                ticksRemaining--;

                if (fadeTicksRemaining > 0)
                {
                    fadeTicksRemaining--;
                }

                if (ticksRemaining <= 0)
                {
                    this.pawn.health.RemoveHediff(this);
                    return;
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
            Scribe_Values.Look(ref fadeTicksRemaining, "fadeTicksRemaining", 0);
            Scribe_Values.Look(ref initialized, "initialized", false);
        }

        public override string LabelInBrackets
        {
            get
            {
                if (Properties != null && ticksRemaining > 0)
                {
                    int secondsRemaining = Mathf.CeilToInt(ticksRemaining / 60f);
                    return secondsRemaining + "s";
                }
                return base.LabelInBrackets;
            }
        }

        public override bool TryMergeWith(Hediff other)
        {
            return false;
        }
    }
}