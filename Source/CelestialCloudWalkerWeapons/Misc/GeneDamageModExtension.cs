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
            if (!dinfo.HasValue) return;

            var ext = Def.GetModExtension<GeneDamageModExtension>();
            if (ext?.applyOnGeneAdd == true)
            {
                HandleDamageFromHediff(dinfo.Value, ext);
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            var ext = Def.GetModExtension<GeneDamageModExtension>();
            if (ext?.continuousDamage == true && parent.ageTicks % ext.damageInterval == 0)
            {
                DoContinuousDamage(ext);
            }
        }

        private void HandleDamageFromHediff(DamageInfo original, GeneDamageModExtension ext)
        {
            if (!HasTargetGene(ext.targetGene)) return;

            float damage = ext.useMultiplier ? original.Amount * ext.damageMultiplier : ext.damageAmount;

            var info = new DamageInfo(ext.damageType, damage, ext.armorPenetration,
                instigator: original.Instigator, hitPart: original.HitPart);

            Pawn.TakeDamage(info);
        }

        private void DoContinuousDamage(GeneDamageModExtension ext)
        {
            if (!HasTargetGene(ext.targetGene)) return;

            var damage = new DamageInfo(ext.damageType, ext.damageAmount, ext.armorPenetration);

            if (ext.targetBodyParts?.Count > 0)
            {
                foreach (var partDef in ext.targetBodyParts)
                {
                    var part = Pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == partDef);
                    if (part != null)
                    {
                        var targeted = damage;
                        targeted.SetHitPart(part);
                        Pawn.TakeDamage(targeted);
                    }
                }
            }
            else
            {
                Pawn.TakeDamage(damage);
            }
        }

        private bool HasTargetGene(string geneName) =>
            Pawn.genes?.GenesListForReading?.Any(g => g.def.defName == geneName) ?? false;
    }

    public class Gene_GeneDamage : Gene
    {
        private GeneDamageModExtension cachedExt;

        private GeneDamageModExtension Ext => cachedExt ?? (cachedExt = def.GetModExtension<GeneDamageModExtension>());

        public override void PostAdd()
        {
            base.PostAdd();
            if (Ext?.applyOnGeneAdd == true)
                CheckAndApplyDamage();
        }

        public override void PostRemove()
        {
            base.PostRemove();
            if (Ext?.applyOnGeneRemove == true)
                DealDamage();
        }

        public override void Tick()
        {
            base.Tick();
            if (Ext?.continuousDamage == true && pawn.IsHashIntervalTick(Ext.damageInterval))
            {
                CheckAndApplyDamage();
            }
        }

        private void CheckAndApplyDamage()
        {
            if (pawn.genes?.GenesListForReading?.Any(g => g.def.defName == Ext.targetGene) == true)
                DealDamage();
        }

        private void DealDamage()
        {
            var info = new DamageInfo(Ext.damageType, Ext.damageAmount, Ext.armorPenetration);

            if (Ext.targetBodyParts?.Count > 0)
            {
                foreach (var partDef in Ext.targetBodyParts)
                {
                    var bodyPart = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == partDef);
                    if (bodyPart != null)
                    {
                        var targeted = info;
                        targeted.SetHitPart(bodyPart);
                        pawn.TakeDamage(targeted);
                    }
                }
            }
            else
            {
                pawn.TakeDamage(info);
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