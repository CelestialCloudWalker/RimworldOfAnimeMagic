using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_AbilityDashPathDamage : CompProperties_AbilityEffect
    {
        public DamageDef damageDef;
        public FloatRange damageAmount = new FloatRange(20, 25);
        public FloatRange armourPen = new FloatRange(0.3f, 0.3f);

        public int width = 3;

        public float splashRadius = 0f;
        public FloatRange splashDamageAmount = new FloatRange(5, 10);
        public DamageDef splashDamageDef;

        public float meleeSkillFactor = 0f;

        public HediffDef hediffOnHit;
        public float hediffSeverity = 0.5f;
        public HediffDef splashHediffOnHit;
        public float splashHediffSeverity = 0.25f;

        public int stunTicksOnHit = 0;

        public DamageDef secondaryDamageDef;
        public FloatRange secondaryDamageAmount = new FloatRange(0, 0);

        public bool canHitFriendly = false;

        public HediffDef hediffOnSelf;
        public float hediffOnSelfSeverity = 1f;

        public EffecterDef castEffecter;
        public EffecterDef hitEffecter;
        public EffecterDef splashEffecter;
        public EffecterDef impactEffecter;

        public CompProperties_AbilityDashPathDamage()
        {
            compClass = typeof(CompAbilityEffect_DashPathDamage);
        }
    }

    public class CompAbilityEffect_DashPathDamage : CompAbilityEffect
    {
        public new CompProperties_AbilityDashPathDamage Props =>
            props as CompProperties_AbilityDashPathDamage;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn pawn = parent?.pawn;
            if (pawn == null) return;

            Map map = pawn.Map;
            if (map == null) return;

            IntVec3 origin = pawn.Position;
            IntVec3 targetCell = target.Cell;

            int skillBonus = 0;
            if (Props.meleeSkillFactor > 0f && pawn.skills != null)
            {
                SkillRecord meleeSkill = pawn.skills.GetSkill(SkillDefOf.Melee);
                if (meleeSkill != null)
                    skillBonus = (int)(meleeSkill.Level * Props.meleeSkillFactor);
            }

            if (Props.hediffOnSelf != null)
                ApplyHediff(pawn, Props.hediffOnSelf, Props.hediffOnSelfSeverity, pawn);

            SpawnEffecter(Props.castEffecter, origin, origin, map);

            DamageDef mainDef = Props.damageDef ?? DamageDefOf.Cut;
            DamageDef splashDef = Props.splashDamageDef ?? mainDef;

            bool anythingHit = false;
            var alreadySplashed = new HashSet<Thing>();

            foreach (IntVec3 cell in GetPathCells(origin, targetCell, map))
            {
                bool cellHit = false;

                foreach (Thing thing in cell.GetThingList(map).ToArray())
                {
                    if (thing == pawn) continue;
                    if (!(thing is Pawn) && !(thing is Building)) continue;
                    if (thing.Destroyed || !thing.Spawned) continue;

                    if (thing is Pawn hitPawn)
                    {
                        if (!hitPawn.HostileTo(pawn) && !Props.canHitFriendly) continue;
                    }

                    float finalAmount = Props.damageAmount.RandomInRange + skillBonus;
                    DealDamage(thing, mainDef, finalAmount, Props.armourPen.RandomInRange, pawn);

                    if (Props.secondaryDamageDef != null && Props.secondaryDamageAmount.max > 0f)
                        DealDamage(thing, Props.secondaryDamageDef,
                            Props.secondaryDamageAmount.RandomInRange,
                            Props.armourPen.RandomInRange, pawn);

                    if (Props.hediffOnHit != null)
                        ApplyHediff(thing, Props.hediffOnHit, Props.hediffSeverity, pawn);

                    if (Props.stunTicksOnHit > 0 && thing is Pawn stunnedPawn)
                    {
                        if (stunnedPawn.stances?.stunner != null)
                            stunnedPawn.stances.stunner.StunFor(Props.stunTicksOnHit, pawn);
                    }

                    alreadySplashed.Add(thing);
                    cellHit = true;
                    anythingHit = true;
                }

                if (cellHit)
                    SpawnEffecter(Props.hitEffecter, cell, cell, map);

                if (Props.splashRadius > 0f && cellHit)
                {
                    foreach (Thing splashThing in GenRadial.RadialDistinctThingsAround(
                        cell, map, Props.splashRadius, true))
                    {
                        if (splashThing == pawn) continue;
                        if (!(splashThing is Pawn)) continue;
                        if (splashThing.Destroyed || !splashThing.Spawned) continue;
                        if (alreadySplashed.Contains(splashThing)) continue;

                        Pawn splashPawn = (Pawn)splashThing;
                        if (!splashPawn.HostileTo(pawn) && !Props.canHitFriendly) continue;

                        DealDamage(splashPawn, splashDef,
                            Props.splashDamageAmount.RandomInRange,
                            Props.armourPen.RandomInRange, pawn);

                        if (Props.splashHediffOnHit != null)
                            ApplyHediff(splashPawn, Props.splashHediffOnHit,
                                Props.splashHediffSeverity, pawn);

                        if (splashPawn.Spawned)
                            SpawnEffecter(Props.splashEffecter,
                                splashPawn.Position, splashPawn.Position, map);

                        alreadySplashed.Add(splashThing);
                    }
                }
            }

            SpawnEffecter(Props.impactEffecter, targetCell, targetCell, map);

            if (Props.impactEffecter == null && anythingHit)
            {
                FleckMaker.Static(targetCell, map, FleckDefOf.ExplosionFlash, 12f);
                FleckMaker.ThrowMicroSparks(targetCell.ToVector3Shifted(), map);
            }
        }

        private void DealDamage(Thing target, DamageDef def, float amount,
            float armorPen, Pawn instigator)
        {
            if (target == null || target.Destroyed) return;
            var dinfo = new DamageInfo(def, amount, armorPen, -1f, instigator,
                null, null, DamageInfo.SourceCategory.ThingOrUnknown, target);
            target.TakeDamage(dinfo);
        }

        private void ApplyHediff(Thing target, HediffDef hediffDef,
            float severity, Pawn instigator)
        {
            if (!(target is Pawn pawn)) return;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (existing != null)
                existing.Severity += severity;
            else
                pawn.health.AddHediff(hediffDef, null,
                    new DamageInfo(DamageDefOf.Cut, 0, 0, -1f, instigator));
        }

        private void SpawnEffecter(EffecterDef def, IntVec3 a, IntVec3 b, Map map)
        {
            if (def == null) return;
            Effecter e = def.Spawn();
            e.Trigger(new TargetInfo(a, map), new TargetInfo(b, map));
            e.Cleanup();
        }

        private IEnumerable<IntVec3> GetPathCells(IntVec3 origin,
            IntVec3 target, Map map)
        {
            Vector3 dir = target.ToVector3Shifted() - origin.ToVector3Shifted();
            float dist = dir.MagnitudeHorizontal();
            if (dist < 0.01f) yield break;

            dir = dir.normalized;
            Vector3 perp = new Vector3(-dir.z, 0f, dir.x);

            var seen = new HashSet<IntVec3>();

            for (float t = 0f; t <= dist; t += 0.5f)
            {
                Vector3 point = origin.ToVector3Shifted() + dir * t;
                for (int w = -(Props.width / 2); w <= Props.width / 2; w++)
                {
                    IntVec3 cell = (point + perp * w).ToIntVec3();
                    if (cell.InBounds(map) && seen.Add(cell))
                        yield return cell;
                }
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return parent?.pawn != null && target.IsValid;
        }
    }
}