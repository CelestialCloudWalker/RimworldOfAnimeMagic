using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AnimeArsenal
{
    public class CompAbilityEffect_DashVitalStrike : CompAbilityEffect
    {
        public new CompProperties_DashVitalStrike Props => (CompProperties_DashVitalStrike)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn casterPawn = parent.pawn;
            if (casterPawn == null || target.Pawn == null) return;

            IntVec3 startPosition = casterPawn.Position;
            Map map = casterPawn.Map;

            if (Props.castEffecter != null)
            {
                Effecter e = Props.castEffecter.Spawn();
                e.Trigger(new TargetInfo(startPosition, map), new TargetInfo(startPosition, map));
                e.Cleanup();
            }

            List<IntVec3> dashPath = GetDashPath(casterPawn, target.Thing.Position, map);
            if (dashPath != null && dashPath.Count > 0)
                ExecuteDash(casterPawn, dashPath, map);

            if (Props.initialStrikeDamage > 0)
                ApplyStrikeDamage(casterPawn, target.Pawn, Props.initialStrikeDamage,
                    Props.initialStrikeArmorPen, Props.damageType);

            ApplyVitalOrganDamage(casterPawn, target.Pawn);

            if (Props.stunDuration > 0)
                ApplyStun(target.Pawn, Props.stunDuration);

            if (Props.effectRadius > 0)
                ApplyAoEDamage(casterPawn, target.Thing.Position);

            if (Props.impactEffecter != null)
            {
                Effecter e = Props.impactEffecter.Spawn();
                e.Trigger(new TargetInfo(target.Thing.Position, map), new TargetInfo(target.Thing.Position, map));
                e.Cleanup();
            }

            SpawnDashEffects(startPosition, target.Thing.Position, dashPath, map);
        }

        private List<IntVec3> GetDashPath(Pawn caster, IntVec3 targetPos, Map map)
        {
            List<IntVec3> path = new List<IntVec3>();
            IntVec3 currentPos = caster.Position;

            Vector3 direction = (targetPos - currentPos).ToVector3();
            float distance = direction.magnitude;
            direction.Normalize();

            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / Props.dashSpeed));

            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 interpolated = currentPos.ToVector3() + direction * distance * t;
                IntVec3 cell = interpolated.ToIntVec3();

                if (cell.InBounds(map) && cell.Standable(map))
                    path.Add(cell);
            }

            IntVec3 finalPos = path.LastOrDefault();
            if (finalPos == default(IntVec3) || !finalPos.Standable(map))
            {
                if (CellFinder.TryFindRandomCellNear(targetPos, map, 2,
                    (IntVec3 c) => c.Standable(map) && c != targetPos, out IntVec3 validCell))
                    path.Add(validCell);
            }

            return path;
        }

        private void ExecuteDash(Pawn caster, List<IntVec3> path, Map map)
        {
            if (path == null || path.Count == 0) return;

            IntVec3 finalPosition = path[path.Count - 1];
            caster.Position = finalPosition;
            caster.Notify_Teleported(false, true);

            if (path.Count > 1)
            {
                IntVec3 raw = path[path.Count - 1] - path[path.Count - 2];
                IntVec3 cardinal = new IntVec3(Math.Sign(raw.x), 0, Math.Sign(raw.z));
                if (cardinal.x != 0 && cardinal.z != 0)
                {
                    if (Math.Abs(raw.x) >= Math.Abs(raw.z))
                        cardinal.z = 0;
                    else
                        cardinal.x = 0;
                }
                if (cardinal != IntVec3.Zero)
                    caster.Rotation = Rot4.FromIntVec3(cardinal);
            }
        }

        private void ApplyStrikeDamage(Pawn instigator, Pawn target, float damage, float armorPen, DamageDef damageType)
        {
            if (target == null || target.Dead) return;

            if (Props.scaleStat != null)
                damage *= instigator.GetStatValue(Props.scaleStat);

            if (Props.additionalDamageFactorFromMeleeSkill > 0)
            {
                float meleeSkill = instigator.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
                damage += damage * (meleeSkill * Props.additionalDamageFactorFromMeleeSkill);
            }

            DamageInfo dinfo = new DamageInfo(damageType, damage, armorPen, -1f, instigator);
            target.TakeDamage(dinfo);
        }

        private void ApplyVitalOrganDamage(Pawn instigator, Pawn target)
        {
            if (target == null || target.Dead) return;
            if (!Props.targetVitalOrgans && (Props.targetBodyParts == null || Props.targetBodyParts.Count == 0))
                return;

            if (!string.IsNullOrEmpty(Props.targetGene))
            {
                bool hasGene = target.genes?.GenesListForReading?.Any(g => g.def.defName == Props.targetGene) ?? false;
                if (!hasGene) return;
            }

            float hitChance = Props.vitalStrikeBaseChance;

            if (Props.scaleChanceByMeleeSkill)
            {
                float attackerMelee = instigator.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0f;
                float defenderMelee = target.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0f;
                hitChance += (attackerMelee - defenderMelee) * 0.02f;
            }

            hitChance = Mathf.Clamp(hitChance, Props.vitalStrikeMinChance, Props.vitalStrikeMaxChance);

            if (Rand.Value > hitChance)
            {
                if (Props.showMissMessage && !string.IsNullOrEmpty(Props.missMessage))
                {
                    string msg = Props.missMessage.Replace("{INSTIGATOR}", instigator.LabelShortCap);
                    Messages.Message(msg, instigator, MessageTypeDefOf.NeutralEvent, false);
                }
                return;
            }

            List<BodyPartRecord> targetParts = GetTargetBodyParts(target);

            if (targetParts?.Count > 0)
            {
                foreach (var part in targetParts)
                {
                    DamageInfo dinfo = new DamageInfo(
                        Props.vitalOrganDamageType ?? Props.damageType,
                        Props.vitalOrganDamage, Props.vitalOrganArmorPen,
                        -1f, instigator, part);
                    target.TakeDamage(dinfo);
                }
            }
            else if (Props.vitalOrganDamage > 0)
            {
                DamageInfo dinfo = new DamageInfo(
                    Props.vitalOrganDamageType ?? Props.damageType,
                    Props.vitalOrganDamage, Props.vitalOrganArmorPen,
                    -1f, instigator);
                target.TakeDamage(dinfo);
            }
        }

        private List<BodyPartRecord> GetTargetBodyParts(Pawn pawn)
        {
            if (Props.targetBodyParts?.Count > 0)
            {
                var parts = new List<BodyPartRecord>();
                foreach (var partDef in Props.targetBodyParts)
                {
                    var part = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def == partDef);
                    if (part != null)
                        parts.Add(part);
                }
                return parts;
            }

            if (Props.targetVitalOrgans)
                return GetVitalOrgans(pawn);

            return null;
        }

        private List<BodyPartRecord> GetVitalOrgans(Pawn pawn)
        {
            var vitalParts = new List<BodyPartRecord>();
            var vitalOrganDefNames = Props.vitalOrganDefNames ?? new List<string>
            {
                "Heart", "Brain", "Liver", "Kidney", "Lung", "Stomach"
            };

            foreach (var part in pawn.RaceProps.body.AllParts)
            {
                if (vitalOrganDefNames.Contains(part.def.defName))
                    vitalParts.Add(part);
            }

            return vitalParts.Count > 0 ? vitalParts : null;
        }

        private void ApplyStun(Pawn target, int duration)
        {
            if (target == null || target.Dead) return;
            target.stances?.stunner?.StunFor(duration, parent.pawn);
        }

        private void ApplyAoEDamage(Pawn instigator, IntVec3 center)
        {
            if (Props.effectRadius <= 0) return;
            Map map = instigator.Map;
            if (map == null) return;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, Props.effectRadius, true))
            {
                if (!cell.InBounds(map)) continue;
                foreach (Thing thing in cell.GetThingList(map))
                {
                    if (thing is Pawn pawn && pawn != instigator && pawn.HostileTo(instigator))
                    {
                        float aoeDamage = Props.initialStrikeDamage * 0.5f;
                        DamageInfo dinfo = new DamageInfo(Props.damageType, aoeDamage,
                            Props.initialStrikeArmorPen * 0.5f, -1f, instigator);
                        pawn.TakeDamage(dinfo);
                    }
                }
            }
        }

        private void SpawnDashEffects(IntVec3 start, IntVec3 end, List<IntVec3> path, Map map)
        {
            if (Props.dashStartMote != null)
                MoteMaker.MakeStaticMote(start, map, Props.dashStartMote);

            if (Props.dashTrailMote != null && path != null)
                foreach (IntVec3 cell in path)
                    MoteMaker.MakeStaticMote(cell, map, Props.dashTrailMote);

            if (Props.dashEndMote != null)
                MoteMaker.MakeStaticMote(end, map, Props.dashEndMote);

            if (Props.dashSound != null)
                Props.dashSound.PlayOneShot(new TargetInfo(end, map));
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.Pawn == null)
            {
                if (throwMessages)
                    Messages.Message("Must target a pawn", MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }
    }

    public class CompProperties_DashVitalStrike : CompProperties_AbilityEffect
    {
        public float dashSpeed = 1.5f;
        public ThingDef dashStartMote;
        public ThingDef dashTrailMote;
        public ThingDef dashEndMote;
        public SoundDef dashSound;
        public DamageDef damageType;
        public float initialStrikeDamage = 10f;
        public float initialStrikeArmorPen = 0.25f;
        public StatDef scaleStat;
        public float additionalDamageFactorFromMeleeSkill = 0f;
        public bool targetVitalOrgans = false;
        public List<BodyPartDef> targetBodyParts;
        public List<string> vitalOrganDefNames;
        public float vitalOrganDamage = 0f;
        public float vitalOrganArmorPen = 0f;
        public DamageDef vitalOrganDamageType;
        public string targetGene;
        public EffecterDef castEffecter;
        public EffecterDef impactEffecter;
        public int stunDuration = 0;
        public float effectRadius = 0f;

        public float vitalStrikeBaseChance = 0.65f;
        public float vitalStrikeMinChance = 0.10f;
        public float vitalStrikeMaxChance = 0.90f;
        public bool scaleChanceByMeleeSkill = true;
        public bool showMissMessage = true;
        public string missMessage = "{INSTIGATOR}'s strike failed to find the neck.";

        public CompProperties_DashVitalStrike()
        {
            compClass = typeof(CompAbilityEffect_DashVitalStrike);
        }
    }
}