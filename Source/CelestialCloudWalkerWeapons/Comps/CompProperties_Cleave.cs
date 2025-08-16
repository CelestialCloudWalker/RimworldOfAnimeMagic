using RimWorld;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using static RimWorld.RitualStage_InteractWithRole;

namespace AnimeArsenal
{
    public class CompProperties_Cleave : CompProperties_AbilityEffect
    {
        public int NumberOfCuts = 8;
        public int KnockbackDistance = 5;
        public float BaseDamage = 8f;
        public int TicksBetweenCuts = 10;
        public DamageDef DamageDef = DamageDefOf.Cut;
        public StatDef ScaleStat = StatDefOf.MeleeDamageFactor;
        public EffecterDef CleaveDamageEffecter;

        public CompProperties_Cleave()
        {
            compClass = typeof(CompAbilityEffect_Cleave);
        }
    }

    public class CompAbilityEffect_Cleave : CompAbilityEffect
    {
        public new CompProperties_Cleave Props => (CompProperties_Cleave)props;
        private Pawn TargetPawn;
        private float DamagePerCut;
        private Ticker DamageTicker;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null)
            {
                return;
            }

            TargetPawn = pawn;
            DamagePerCut = Props.BaseDamage;

            DamageTicker = new Ticker(Props.TicksBetweenCuts, ApplyCut, true, Props.NumberOfCuts);

            IntVec3 launchDirection = pawn.Position - parent.pawn.Position;
            IntVec3 destination = pawn.Position + launchDirection * Props.KnockbackDistance;
            PawnFlyer pawnFlyer = PawnFlyer.MakeFlyer(CelestialDefof.AnimeArsenal_Flyer, pawn, destination, null, null);
            GenSpawn.Spawn(pawnFlyer, destination, parent.pawn.Map);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (DamageTicker != null && DamageTicker.IsRunning)
            {
                DamageTicker.Tick();
            }
        }

        private void ApplyCut()
        {
            if (TargetPawn == null || TargetPawn.Dead || TargetPawn.Destroyed)
            {
                DamageTicker?.Stop();
                return;
            }

            if (this.Props.CleaveDamageEffecter != null)
            {
                this.Props.CleaveDamageEffecter.SpawnMaintained(TargetPawn, TargetPawn.MapHeld);
            }

            
            float damageMultiplier = 1f;
            if (Props.ScaleStat != null)
            {
                damageMultiplier = TargetPawn.GetStatValue(Props.ScaleStat);
            }

            float actualDamage = DamagePerCut * damageMultiplier;
            DamageInfo damageInfo = new DamageInfo(Props.DamageDef ?? DamageDefOf.Cut, actualDamage);
            TargetPawn.TakeDamage(damageInfo);
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref TargetPawn, "targetPawn");
            Scribe_Values.Look(ref DamagePerCut, "damagePerCut");
            Scribe_Deep.Look(ref DamageTicker, "damageTicker");
        }
    }
}