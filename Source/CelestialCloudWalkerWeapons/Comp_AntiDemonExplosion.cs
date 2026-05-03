using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AnimeArsenal
{
    public class CompProperties_AntiDemonExplosion : CompProperties
    {
        public List<GeneDef> targetGenes = new List<GeneDef>();
        public DamageDef damageDef = null;
        public float damageAmount = 25f;
        public float armorPenetration = 0.3f;
        public float explosionRadius = 3f;
        public HediffDef applyHediff = null;
        public float hediffSeverity = 0.5f;
        public EffecterDef impactEffecter = null;
        public EffecterDef explosionEffecter = null;
        public SoundDef explosionSound = null;
        public bool onlyHostileBuildings = true;

        public CompProperties_AntiDemonExplosion()
        {
            compClass = typeof(Comp_AntiDemonExplosion);
        }
    }

    public class Comp_AntiDemonExplosion : ThingComp
    {
        public CompProperties_AntiDemonExplosion Props =>
            (CompProperties_AntiDemonExplosion)props;

        public Pawn Caster { get; set; }

        public bool PawnMatchesGeneFilter(Pawn pawn)
        {
            if (pawn?.genes == null) return false;
            if (Props.targetGenes == null || Props.targetGenes.Count == 0) return true;
            foreach (GeneDef gene in Props.targetGenes)
                if (pawn.genes.HasActiveGene(gene)) return true;
            return false;
        }

        private bool BuildingShouldBeHit(Building building)
        {
            if (building == null || building.Destroyed) return false;
            if (!Props.onlyHostileBuildings) return true;
            if (building.Faction == null) return true;
            Faction casterFaction = Caster?.Faction ?? Faction.OfPlayerSilentFail;
            if (casterFaction == null) return true;
            return building.Faction.HostileTo(casterFaction);
        }

        public void Detonate(IntVec3 center, Map map)
        {
            if (map == null) return;

            SpawnImpactEffecter(center, map);
            PlayEffects(center, map);

            DamageDef dmgDef = Props.damageDef ?? DamageDefOf.Bomb;
            List<Thing> snapshot = BuildSnapshot(center, map);

            foreach (Thing thing in snapshot)
            {
                if (thing is Pawn pawn)
                {
                    if (pawn.Dead || pawn.Destroyed) continue;
                    if (PawnMatchesGeneFilter(pawn))
                    {
                        ApplyDamageToPawn(pawn, dmgDef, center);
                        TryApplyHediff(pawn);
                    }
                }
                else if (thing is Building building)
                {
                    if (!building.Destroyed && BuildingShouldBeHit(building))
                        ApplyDamageToThing(building, dmgDef);
                }
            }
        }

        private List<Thing> BuildSnapshot(IntVec3 center, Map map)
        {
            List<Thing> results = new List<Thing>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, Props.explosionRadius, true))
            {
                if (!cell.InBounds(map)) continue;
                foreach (Thing thing in cell.GetThingList(map).ToList())
                    if (!results.Contains(thing))
                        results.Add(thing);
            }
            return results;
        }

        private void ApplyDamageToPawn(Pawn pawn, DamageDef dmgDef, IntVec3 center)
        {
            float dist = pawn.Position.DistanceTo(center);
            float falloff = Mathf.Clamp01(1f - (dist / Mathf.Max(Props.explosionRadius, 1f)));
            float finalDmg = Mathf.Max(1f, Props.damageAmount * (0.5f + 0.5f * falloff));
            DamageInfo dinfo = new DamageInfo(
                dmgDef, finalDmg, Props.armorPenetration,
                -1f, Caster, null, null,
                DamageInfo.SourceCategory.ThingOrUnknown);
            pawn.TakeDamage(dinfo);
        }

        private void ApplyDamageToThing(Thing thing, DamageDef dmgDef)
        {
            DamageInfo dinfo = new DamageInfo(
                dmgDef, Props.damageAmount, Props.armorPenetration,
                -1f, Caster, null, null,
                DamageInfo.SourceCategory.ThingOrUnknown);
            thing.TakeDamage(dinfo);
        }

        private void TryApplyHediff(Pawn pawn)
        {
            if (Props.applyHediff == null || pawn.Dead || pawn.Destroyed) return;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(Props.applyHediff);
            if (existing != null)
            {
                existing.Severity = System.Math.Min(
                    existing.Severity + Props.hediffSeverity,
                    existing.def.maxSeverity > 0f ? existing.def.maxSeverity : 1f);
            }
            else
            {
                Hediff hediff = HediffMaker.MakeHediff(Props.applyHediff, pawn);
                hediff.Severity = Props.hediffSeverity;
                pawn.health.AddHediff(hediff);
            }
        }

        private void SpawnImpactEffecter(IntVec3 center, Map map)
        {
            if (Props.impactEffecter == null) return;
            Effecter e = Props.impactEffecter.Spawn(center, map);
            e.Cleanup();
        }

        private void PlayEffects(IntVec3 center, Map map)
        {
            Props.explosionSound?.PlayOneShot(new TargetInfo(center, map));
            if (Props.explosionEffecter != null)
            {
                Effecter e = Props.explosionEffecter.Spawn(center, map);
                e.Cleanup();
            }
        }
    }

    public static class AntiDemonBombUtility
    {
        public static void DetonateAt(
            IntVec3 cell,
            Map map,
            Pawn caster,
            List<GeneDef> targetGenes,
            DamageDef damageDef = null,
            float damage = 25f,
            float radius = 3f,
            float armorPen = 0.3f,
            HediffDef applyHediff = null,
            float hediffSeverity = 0.5f,
            EffecterDef impactEffecter = null,
            EffecterDef explosionEffecter = null,
            SoundDef sound = null,
            bool onlyHostileBuildings = true)
        {
            if (map == null || !cell.InBounds(map)) return;

            if (impactEffecter != null)
            {
                Effecter e = impactEffecter.Spawn(cell, map);
                e.Cleanup();
            }

            sound?.PlayOneShot(new TargetInfo(cell, map));

            DamageDef dmgDef = damageDef ?? DamageDefOf.Bomb;

            List<Thing> snapshot = new List<Thing>();
            foreach (IntVec3 c in GenRadial.RadialCellsAround(cell, radius, true))
            {
                if (!c.InBounds(map)) continue;
                foreach (Thing t in c.GetThingList(map).ToList())
                    if (!snapshot.Contains(t))
                        snapshot.Add(t);
            }

            foreach (Thing thing in snapshot)
            {
                if (thing is Pawn pawn)
                {
                    if (pawn.Dead || pawn.Destroyed) continue;

                    bool hit = targetGenes == null || targetGenes.Count == 0;
                    if (!hit && pawn.genes != null)
                        foreach (GeneDef gene in targetGenes)
                            if (pawn.genes.HasActiveGene(gene)) { hit = true; break; }

                    if (!hit) continue;

                    float dist = pawn.Position.DistanceTo(cell);
                    float falloff = Mathf.Clamp01(1f - (dist / Mathf.Max(radius, 1f)));
                    float finalDmg = Mathf.Max(1f, damage * (0.5f + 0.5f * falloff));

                    DamageInfo dinfo = new DamageInfo(
                        dmgDef, finalDmg, armorPen,
                        -1f, caster, null, null,
                        DamageInfo.SourceCategory.ThingOrUnknown);
                    pawn.TakeDamage(dinfo);

                    if (applyHediff != null && !pawn.Dead && !pawn.Destroyed)
                    {
                        Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(applyHediff);
                        if (existing != null)
                        {
                            existing.Severity = System.Math.Min(
                                existing.Severity + hediffSeverity,
                                existing.def.maxSeverity > 0f ? existing.def.maxSeverity : 1f);
                        }
                        else
                        {
                            Hediff h = HediffMaker.MakeHediff(applyHediff, pawn);
                            h.Severity = hediffSeverity;
                            pawn.health.AddHediff(h);
                        }
                    }
                }
                else if (thing is Building building && !building.Destroyed)
                {
                    bool hostile = !onlyHostileBuildings
                        || building.Faction == null
                        || (caster?.Faction != null && building.Faction.HostileTo(caster.Faction));

                    if (hostile)
                    {
                        DamageInfo dinfo = new DamageInfo(
                            dmgDef, damage, armorPen,
                            -1f, caster, null, null,
                            DamageInfo.SourceCategory.ThingOrUnknown);
                        building.TakeDamage(dinfo);
                    }
                }
            }

            if (explosionEffecter != null)
            {
                Effecter e = explosionEffecter.Spawn(cell, map);
                e.Cleanup();
            }
        }
    }
}