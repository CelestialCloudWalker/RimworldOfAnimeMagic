using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace AnimeArsenal
{
    public class GeneDamageModExtension : DefModExtension
    {
        public DamageDef damageType;

        public float damageAmount = 1f;

        public string targetGene;

        public float damageMultiplier = 1f;

        public bool useMultiplier = false;

        public List<BodyPartDef> targetBodyParts;

        public float armorPenetration = 0f;

        public bool continuousDamage = false;
        public int damageInterval = 60;
        public bool applyOnGeneAdd = false;
        public bool applyOnGeneRemove = false;

        public bool damageOnHit = true; 
    }

    public class HediffComp_GeneDamage : HediffComp
    {
        public HediffCompProperties_GeneDamage Props => (HediffCompProperties_GeneDamage)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            if (dinfo.HasValue)
            {
                GeneDamageModExtension modExt = Def.GetModExtension<GeneDamageModExtension>();
                if (modExt != null && modExt.applyOnGeneAdd)
                {
                    ProcessGeneDamageForHediff(dinfo.Value, modExt);
                }
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            GeneDamageModExtension modExt = Def.GetModExtension<GeneDamageModExtension>();
            if (modExt != null && modExt.continuousDamage)
            {
                if (parent.ageTicks % modExt.damageInterval == 0)
                {
                    ProcessContinuousGeneDamage(modExt);
                }
            }
        }

        private void ProcessGeneDamageForHediff(DamageInfo dinfo, GeneDamageModExtension modExt)
        {
            if (!(Pawn.genes?.GenesListForReading?.Any(g => g.def.defName == modExt.targetGene) ?? false))
                return;

            float extraDamage = modExt.useMultiplier ?
                dinfo.Amount * modExt.damageMultiplier :
                modExt.damageAmount;

            DamageInfo extraDamageInfo = new DamageInfo(
                modExt.damageType,
                extraDamage,
                modExt.armorPenetration,
                instigator: dinfo.Instigator,
                hitPart: dinfo.HitPart
            );

            Pawn.TakeDamage(extraDamageInfo);
        }

        private void ProcessContinuousGeneDamage(GeneDamageModExtension modExt)
        {
            if (!(Pawn.genes?.GenesListForReading?.Any(g => g.def.defName == modExt.targetGene) ?? false))
                return;

            DamageInfo damageInfo = new DamageInfo(
                modExt.damageType,
                modExt.damageAmount,
                modExt.armorPenetration
            );

            if (modExt.targetBodyParts != null && modExt.targetBodyParts.Count > 0)
            {
                foreach (BodyPartDef bodyPartDef in modExt.targetBodyParts)
                {
                    BodyPartRecord bodyPart = Pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == bodyPartDef);
                    if (bodyPart != null)
                    {
                        DamageInfo targetedDamage = damageInfo;
                        targetedDamage.SetHitPart(bodyPart);
                        Pawn.TakeDamage(targetedDamage);
                    }
                }
            }
            else
            {
                Pawn.TakeDamage(damageInfo);
            }
        }
    }

    public class Gene_GeneDamage : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            GeneDamageModExtension modExt = def.GetModExtension<GeneDamageModExtension>();
            if (modExt != null && modExt.applyOnGeneAdd)
            {
                ProcessGeneAddDamage(modExt);
            }
        }

        public override void PostRemove()
        {
            base.PostRemove();
            GeneDamageModExtension modExt = def.GetModExtension<GeneDamageModExtension>();
            if (modExt != null && modExt.applyOnGeneRemove)
            {
                ProcessGeneRemoveDamage(modExt);
            }
        }

        public override void Tick()
        {
            base.Tick();
            GeneDamageModExtension modExt = def.GetModExtension<GeneDamageModExtension>();
            if (modExt != null && modExt.continuousDamage)
            {
                if (pawn.IsHashIntervalTick(modExt.damageInterval))
                {
                    ProcessContinuousGeneDamage(modExt);
                }
            }
        }

        private void ProcessGeneAddDamage(GeneDamageModExtension modExt)
        {
            if (pawn.genes?.GenesListForReading?.Any(g => g.def.defName == modExt.targetGene) ?? false)
            {
                ApplyGeneDamage(modExt);
            }
        }

        private void ProcessGeneRemoveDamage(GeneDamageModExtension modExt)
        {
            ApplyGeneDamage(modExt);
        }

        private void ProcessContinuousGeneDamage(GeneDamageModExtension modExt)
        {
            if (pawn.genes?.GenesListForReading?.Any(g => g.def.defName == modExt.targetGene) ?? false)
            {
                ApplyGeneDamage(modExt);
            }
        }

        private void ApplyGeneDamage(GeneDamageModExtension modExt)
        {
            DamageInfo damageInfo = new DamageInfo(
                modExt.damageType,
                modExt.damageAmount,
                modExt.armorPenetration
            );

            if (modExt.targetBodyParts != null && modExt.targetBodyParts.Count > 0)
            {
                foreach (BodyPartDef bodyPartDef in modExt.targetBodyParts)
                {
                    BodyPartRecord bodyPart = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == bodyPartDef);
                    if (bodyPart != null)
                    {
                        DamageInfo targetedDamage = damageInfo;
                        targetedDamage.SetHitPart(bodyPart);
                        pawn.TakeDamage(targetedDamage);
                    }
                }
            }
            else
            {
                pawn.TakeDamage(damageInfo);
            }
        }
    }

    public class HediffCompProperties_GeneDamage : HediffCompProperties
    {
        public HediffCompProperties_GeneDamage()
        {
            compClass = typeof(HediffComp_GeneDamage);
        }
    }
}