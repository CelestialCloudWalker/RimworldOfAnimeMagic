using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_AbilityStealBite : CompProperties_AbilityEffect
    {
        public DamageDef damageType = DamageDefOf.Bite;
        public float baseDamage = 8f;
        public float armorPenetration = 0f;
        public bool scaleDamageWithMelee = true;

        public CompProperties_AbilityStealBite()
        {
            compClass = typeof(CompAbilityStealBite);
        }
    }

    public class CompAbilityStealBite : CompAbilityEffect
    {
        private new CompProperties_AbilityStealBite Props => (CompProperties_AbilityStealBite)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (target.Pawn?.Dead != false || parent.pawn == null)
            {
                Messages.Message("AbilityStealBite_TargetDead".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Pawn caster = parent.pawn;
            Pawn victim = target.Pawn;

            AbilityDef copiedAbility = GetRandomAbility(victim);
            if (copiedAbility == null)
            {
                Messages.Message("AbilityStealBite_NoAbilities".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            float successChance = caster.GetStatValue(DefDatabase<StatDef>.GetNamed("AbilityStealChance"));
            if (Rand.Value > successChance)
            {
                Messages.Message("AbilityStealBite_Failed".Translate(caster.LabelShort), MessageTypeDefOf.NegativeEvent);
                return;
            }

            AddAbilityToPawn(caster, copiedAbility);
            DealBiteDamage(caster, victim);

            AddThought(caster, "CopiedAbility");
            AddThought(victim, "AbilityCopied");

            FleckMaker.ThrowDustPuffThick(victim.Position.ToVector3(), victim.Map, 2f, Color.red);
            FleckMaker.ThrowDustPuffThick(caster.Position.ToVector3(), caster.Map, 2f, Color.green);

            Messages.Message("AbilityStealBite_Success".Translate(caster.LabelShort, copiedAbility.LabelCap),
                MessageTypeDefOf.PositiveEvent);
        }

        private void DealBiteDamage(Pawn caster, Pawn victim)
        {
            float damage = Props.baseDamage;
            if (Props.scaleDamageWithMelee)
                damage *= caster.GetStatValue(StatDefOf.MeleeDamageFactor);

            var biteInfo = new DamageInfo(Props.damageType, Mathf.RoundToInt(damage));
            biteInfo.SetInstantPermanentInjury(false);
            victim.TakeDamage(biteInfo);
        }

        private void AddThought(Pawn pawn, string thoughtDefName)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null) return;

            var thoughtDef = DefDatabase<ThoughtDef>.GetNamed(thoughtDefName);
            if (thoughtDef?.stages?.Count > 0)
                pawn.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
        }

        private AbilityDef GetRandomAbility(Pawn pawn)
        {
            if (pawn.abilities?.abilities == null) return null;

            var abilities = pawn.abilities.abilities.Where(a => a?.def != null).Select(a => a.def).ToList();
            return abilities.Count > 0 ? abilities.RandomElement() : null;
        }

        private void AddAbilityToPawn(Pawn pawn, AbilityDef ability)
        {
            if (pawn.abilities == null)
                pawn.abilities = new Pawn_AbilityTracker(pawn);

            if (!pawn.abilities.abilities.Any(a => a?.def == ability))
                pawn.abilities.GainAbility(ability);

            var hediffDef = DefDatabase<HediffDef>.GetNamed("CopiedAbilityHediff");
            if (hediffDef != null)
            {
                var hediff = (HediffCopiedAbility)HediffMaker.MakeHediff(hediffDef, pawn);
                hediff.copiedAbility = ability;
                pawn.health.AddHediff(hediff);
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) return false;

            if (target.Pawn?.Dead != false)
            {
                if (throwMessages)
                    Messages.Message("AbilityStealBite_TargetDead".Translate(), MessageTypeDefOf.RejectInput);
                return false;
            }

            return true;
        }
    }

    public class HediffCopiedAbility : Hediff
    {
        public AbilityDef copiedAbility;

        public override string LabelInBrackets => copiedAbility?.LabelCap ?? "unknown";

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref copiedAbility, "copiedAbility");
        }

        public override void PostRemoved()
        {
            base.PostRemoved();

            if (copiedAbility != null && pawn?.abilities?.abilities != null)
            {
                var ability = pawn.abilities.abilities.FirstOrDefault(a => a.def == copiedAbility);
                if (ability != null)
                    pawn.abilities.abilities.Remove(ability);
            }
        }
    }
}