using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;
using HarmonyLib;

namespace AnimeArsenal
{
    public class SelflessStateProperties : DefModExtension
    {
        public int durationTicks = 1000;
    }

    public class Hediff_SelflessState : HediffWithComps
    {
        private int ticksRemaining;
        private bool initialized = false;
        private SelflessStateProperties Properties => def.GetModExtension<SelflessStateProperties>();

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
            if (pawn == null || !pawn.Spawned) return;
            if (Properties != null && ticksRemaining > 0)
            {
                ticksRemaining--;
                if (ticksRemaining <= 0)
                {
                    pawn.health.RemoveHediff(this);
                    return;
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
                if (Properties != null && ticksRemaining > 0)
                    return Mathf.CeilToInt(ticksRemaining / 60f) + "s";
                return base.LabelInBrackets;
            }
        }

        public override bool TryMergeWith(Hediff other) => false;
    }
}