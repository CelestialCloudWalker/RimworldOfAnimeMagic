using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace AnimeArsenal
{
    // ModExtension class to define extra damage against genes
    public class GeneDamageModExtension : DefModExtension
    {
        // The damage type to apply (e.g., "Cut", "Blunt", etc.)
        public DamageDef damageType;

        // Amount of extra damage to deal
        public float damageAmount = 1f;

        // The target gene defName
        public string targetGene;

        // Optional: damage multiplier instead of flat damage
        public float damageMultiplier = 1f;

        // Optional: whether to use multiplier (true) or flat damage (false)
        public bool useMultiplier = false;

        // Optional: body parts to target specifically
        public List<BodyPartDef> targetBodyParts;

        // Optional: armor penetration for the extra damage
        public float armorPenetration = 0f;

        // Additional options for hediffs and genes
        public bool continuousDamage = false;
        public int damageInterval = 60; // ticks between damage applications
        public bool applyOnGeneAdd = false;
        public bool applyOnGeneRemove = false;

        // New option for damage-on-hit for gene carriers
        public bool damageOnHit = true; // Default behavior - damage when hit by weapons/projectiles with this modExtension
    }

    // HediffComp for continuous damage from hediffs
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

    // Gene class for gene-specific behavior
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
            // Apply damage when THIS gene is added, checking for the target gene
            if (pawn.genes?.GenesListForReading?.Any(g => g.def.defName == modExt.targetGene) ?? false)
            {
                ApplyGeneDamage(modExt);
            }
        }

        private void ProcessGeneRemoveDamage(GeneDamageModExtension modExt)
        {
            // Apply damage when THIS gene is removed
            ApplyGeneDamage(modExt);
        }

        private void ProcessContinuousGeneDamage(GeneDamageModExtension modExt)
        {
            // Apply continuous damage if target gene is present
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

    // Properties classes for hediffs
    public class HediffCompProperties_GeneDamage : HediffCompProperties
    {
        public HediffCompProperties_GeneDamage()
        {
            compClass = typeof(HediffComp_GeneDamage);
        }
    }
}