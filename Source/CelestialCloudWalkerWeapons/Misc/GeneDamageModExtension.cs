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
        public bool targetVitalOrgans = false; 
        public List<string> vitalOrganDefNames; 
        public float armorPenetration = 0f;
        public bool continuousDamage = false;
        public int damageInterval = 60;
        public int damageDelay = 0;
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
            if (!ShouldApplyDamage(ext.targetGene)) return;

            float damage = ext.useMultiplier ? original.Amount * ext.damageMultiplier : ext.damageAmount;

            var info = new DamageInfo(ext.damageType, damage, ext.armorPenetration,
                instigator: original.Instigator, hitPart: original.HitPart);

            Pawn.TakeDamage(info);
        }

        private void DoContinuousDamage(GeneDamageModExtension ext)
        {
            if (!ShouldApplyDamage(ext.targetGene)) return;

            var damage = new DamageInfo(ext.damageType, ext.damageAmount, ext.armorPenetration);

            ApplyDamageToBodyParts(ext, damage);
        }

        private void ApplyDamageToBodyParts(GeneDamageModExtension ext, DamageInfo damage)
        {
            List<BodyPartRecord> targetParts = GetTargetBodyParts(ext);

            if (targetParts?.Count > 0)
            {
                foreach (var part in targetParts)
                {
                    var targeted = damage;
                    targeted.SetHitPart(part);
                    Pawn.TakeDamage(targeted);
                }
            }
            else
            {
                Pawn.TakeDamage(damage);
            }
        }

        private List<BodyPartRecord> GetTargetBodyParts(GeneDamageModExtension ext)
        {
            if (ext.targetBodyParts?.Count > 0)
            {
                var parts = new List<BodyPartRecord>();
                foreach (var partDef in ext.targetBodyParts)
                {
                    var part = Pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == partDef);
                    if (part != null)
                        parts.Add(part);
                }
                return parts;
            }

            if (ext.targetVitalOrgans)
            {
                return GetVitalOrgans(Pawn, ext);
            }

            return null;
        }

        private List<BodyPartRecord> GetVitalOrgans(Pawn pawn, GeneDamageModExtension ext)
        {
            var vitalParts = new List<BodyPartRecord>();

            var vitalOrganDefNames = ext?.vitalOrganDefNames ?? new List<string>
            {
                "Heart",
                "Brain",
                "Liver",
                "Kidney",
                "Lung",
                "Stomach"
            };

            foreach (var part in pawn.RaceProps.body.AllParts)
            {
                if (vitalOrganDefNames.Contains(part.def.defName))
                {
                    vitalParts.Add(part);
                }
            }

            return vitalParts.Count > 0 ? vitalParts : null;
        }

        private bool ShouldApplyDamage(string geneName)
        {
            if (string.IsNullOrEmpty(geneName))
                return true;

            return Pawn.genes?.GenesListForReading?.Any(g => g.def.defName == geneName) ?? false;
        }
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
            if (ShouldApplyDamage(Ext.targetGene))
                DealDamage();
        }

        private bool ShouldApplyDamage(string geneName)
        {
            if (string.IsNullOrEmpty(geneName))
                return true;

            return pawn.genes?.GenesListForReading?.Any(g => g.def.defName == geneName) ?? false;
        }

        private void DealDamage()
        {
            var info = new DamageInfo(Ext.damageType, Ext.damageAmount, Ext.armorPenetration);

            List<BodyPartRecord> targetParts = GetTargetBodyParts(Ext);

            if (targetParts?.Count > 0)
            {
                foreach (var part in targetParts)
                {
                    var targeted = info;
                    targeted.SetHitPart(part);
                    pawn.TakeDamage(targeted);
                }
            }
            else
            {
                pawn.TakeDamage(info);
            }
        }

        private List<BodyPartRecord> GetTargetBodyParts(GeneDamageModExtension ext)
        {
            if (ext.targetBodyParts?.Count > 0)
            {
                var parts = new List<BodyPartRecord>();
                foreach (var partDef in ext.targetBodyParts)
                {
                    var bodyPart = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == partDef);
                    if (bodyPart != null)
                        parts.Add(bodyPart);
                }
                return parts;
            }

            if (ext.targetVitalOrgans)
            {
                return GetVitalOrgans(pawn, ext);
            }

            return null;
        }

        private List<BodyPartRecord> GetVitalOrgans(Pawn pawn, GeneDamageModExtension ext)
        {
            var vitalParts = new List<BodyPartRecord>();

            var vitalOrganDefNames = ext?.vitalOrganDefNames ?? new List<string>
            {
                "Heart",
                "Brain",
                "Liver",
                "Kidney",
                "Lung",
                "Stomach"
            };

            foreach (var part in pawn.RaceProps.body.AllParts)
            {
                if (vitalOrganDefNames.Contains(part.def.defName))
                {
                    vitalParts.Add(part);
                }
            }

            return vitalParts.Count > 0 ? vitalParts : null;
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