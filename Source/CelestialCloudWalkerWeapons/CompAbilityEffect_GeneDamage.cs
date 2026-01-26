using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace AnimeArsenal
{
    public class CompAbilityEffect_GeneDamage : CompAbilityEffect
    {
        public new CompProperties_AbilityGeneDamage Props => (CompProperties_AbilityGeneDamage)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (target.Pawn == null) return;

            GeneDamageModExtension ext = null;

            if (parent is TalentedLevelingAbility levelingAbility)
            {
                ext = levelingAbility.GetModExtension<GeneDamageModExtension>();
            }
            else
            {
                ext = parent.def.GetModExtension<GeneDamageModExtension>();
            }

            if (ext == null) return;

            if (!ShouldApplyDamage(target.Pawn, ext.targetGene)) return;

            if (ext.damageDelay > 0)
            {
                QueueDelayedDamage(target.Pawn, ext);
            }
            else
            {
                ApplyDamageToTarget(target.Pawn, ext);
            }
        }

        private void QueueDelayedDamage(Pawn targetPawn, GeneDamageModExtension ext)
        {
            var map = targetPawn.Map;
            if (map == null) return;

            var delayedComp = map.GetComponent<MapComponent_DelayedGeneDamage>();
            if (delayedComp == null)
            {
                delayedComp = new MapComponent_DelayedGeneDamage(map);
                map.components.Add(delayedComp);
            }

            delayedComp.AddDelayedDamage(targetPawn, ext, parent.pawn, ext.damageDelay);
        }

        private void ApplyDamageToTarget(Pawn targetPawn, GeneDamageModExtension ext)
        {
            if (targetPawn == null || targetPawn.Destroyed || targetPawn.Dead) return;

            var damage = new DamageInfo(ext.damageType, ext.damageAmount, ext.armorPenetration,
                instigator: parent.pawn);

            List<BodyPartRecord> targetParts = GetTargetBodyParts(targetPawn, ext);

            if (targetParts?.Count > 0)
            {
                foreach (var part in targetParts)
                {
                    var targeted = damage;
                    targeted.SetHitPart(part);
                    targetPawn.TakeDamage(targeted);
                }
            }
            else
            {
                targetPawn.TakeDamage(damage);
            }
        }

        private List<BodyPartRecord> GetTargetBodyParts(Pawn pawn, GeneDamageModExtension ext)
        {
            if (ext.targetBodyParts?.Count > 0)
            {
                var parts = new List<BodyPartRecord>();
                foreach (var partDef in ext.targetBodyParts)
                {
                    var part = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == partDef);
                    if (part != null)
                        parts.Add(part);
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
                "Heart", "Brain", "Liver", "Kidney", "Lung", "Stomach"
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

        private bool ShouldApplyDamage(Pawn pawn, string geneName)
        {
            if (string.IsNullOrEmpty(geneName))
                return true;

            return pawn.genes?.GenesListForReading?.Any(g => g.def.defName == geneName) ?? false;
        }

        public static void ApplyDelayedDamage(Pawn targetPawn, GeneDamageModExtension ext, Pawn instigator)
        {
            if (targetPawn == null || targetPawn.Destroyed || targetPawn.Dead) return;

            var damage = new DamageInfo(ext.damageType, ext.damageAmount, ext.armorPenetration,
                instigator: instigator);

            List<BodyPartRecord> targetParts = GetTargetBodyPartsStatic(targetPawn, ext);

            if (targetParts?.Count > 0)
            {
                foreach (var part in targetParts)
                {
                    var targeted = damage;
                    targeted.SetHitPart(part);
                    targetPawn.TakeDamage(targeted);
                }
            }
            else
            {
                targetPawn.TakeDamage(damage);
            }
        }

        private static List<BodyPartRecord> GetTargetBodyPartsStatic(Pawn pawn, GeneDamageModExtension ext)
        {
            if (ext.targetBodyParts?.Count > 0)
            {
                var parts = new List<BodyPartRecord>();
                foreach (var partDef in ext.targetBodyParts)
                {
                    var part = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == partDef);
                    if (part != null)
                        parts.Add(part);
                }
                return parts;
            }

            if (ext.targetVitalOrgans)
            {
                var vitalParts = new List<BodyPartRecord>();
                var vitalOrganDefNames = ext?.vitalOrganDefNames ?? new List<string>
                {
                    "Heart", "Brain", "Liver", "Kidney", "Lung", "Stomach"
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

            return null;
        }
    }

    public class CompProperties_AbilityGeneDamage : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityGeneDamage()
        {
            compClass = typeof(CompAbilityEffect_GeneDamage);
        }
    }

    public class MapComponent_DelayedGeneDamage : MapComponent
    {
        private class DelayedDamageInfo
        {
            public Pawn target;
            public GeneDamageModExtension extension;
            public Pawn instigator;
            public int ticksRemaining;
        }

        private List<DelayedDamageInfo> delayedDamages = new List<DelayedDamageInfo>();

        public MapComponent_DelayedGeneDamage(Map map) : base(map) { }

        public void AddDelayedDamage(Pawn target, GeneDamageModExtension ext, Pawn instigator, int delay)
        {
            delayedDamages.Add(new DelayedDamageInfo
            {
                target = target,
                extension = ext,
                instigator = instigator,
                ticksRemaining = delay
            });
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            for (int i = delayedDamages.Count - 1; i >= 0; i--)
            {
                var info = delayedDamages[i];
                info.ticksRemaining--;

                if (info.ticksRemaining <= 0)
                {
                    CompAbilityEffect_GeneDamage.ApplyDelayedDamage(info.target, info.extension, info.instigator);
                    delayedDamages.RemoveAt(i);
                }
            }
        }
    }
}