using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_WintryIcicles : CompProperties_AbilityEffect
    {
        public float radius = 6f;
        public int icicleCount = 8;
        public int maxTargetsHit = -1;
        public int damageAmount = 12;
        public DamageDef damageDef;
        public float armorPen = 0f;
        public HediffDef hediffOnHit;
        public float hediffSeverity = 0f;
        public EffecterDef icicleEffecter;
        public FleckDef icicleFleck;
        public ThingDef icicleMote;
        public float moteScale = 1f;

        public CompProperties_WintryIcicles()
        {
            compClass = typeof(CompAbilityEffect_WintryIcicles);
        }
    }

    public class CompAbilityEffect_WintryIcicles : CompAbilityEffect
    {
        public new CompProperties_WintryIcicles Props => (CompProperties_WintryIcicles)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster?.Map == null) return;

            Map map = caster.Map;

            List<Pawn> pawnsInRange = map.mapPawns.AllPawnsSpawned
                .Where(p => p != caster
                            && !p.Dead
                            && p.Position.InHorDistOf(target.Cell, Props.radius)
                            && p.Position.InBounds(map))
                .InRandomOrder()
                .ToList();

            List<Pawn> pawnsToHit = Props.maxTargetsHit >= 0
                ? pawnsInRange.Take(Props.maxTargetsHit).ToList()
                : pawnsInRange;

            DamageDef dmgDef = Props.damageDef ?? DamageDefOf.Blunt;

            foreach (Pawn hitPawn in pawnsToHit)
            {
                SpawnVisuals(hitPawn.Position, map);

                DamageInfo dinfo = new DamageInfo(dmgDef, Props.damageAmount, Props.armorPen, -1f, caster);
                hitPawn.TakeDamage(dinfo);

                if (Props.hediffOnHit != null && Props.hediffSeverity > 0f)
                {
                    Hediff existing = hitPawn.health.hediffSet.GetFirstHediffOfDef(Props.hediffOnHit);
                    if (existing != null)
                        existing.Severity += Props.hediffSeverity;
                    else
                    {
                        Hediff newHediff = HediffMaker.MakeHediff(Props.hediffOnHit, hitPawn);
                        newHediff.Severity = Props.hediffSeverity;
                        hitPawn.health.AddHediff(newHediff);
                    }
                }
            }

            HashSet<IntVec3> pawnCells = new HashSet<IntVec3>(pawnsToHit.Select(p => p.Position));
            int extraVisuals = Mathf.Max(0, Props.icicleCount - pawnsToHit.Count);

            if (extraVisuals > 0)
            {
                List<IntVec3> emptyCells = GenRadial
                    .RadialCellsAround(target.Cell, Props.radius, true)
                    .Where(c => c.InBounds(map) && c.Walkable(map) && !pawnCells.Contains(c))
                    .InRandomOrder()
                    .Take(extraVisuals)
                    .ToList();

                foreach (IntVec3 cell in emptyCells)
                    SpawnVisuals(cell, map);
            }
        }

        private void SpawnVisuals(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map)) return;

            if (Props.icicleEffecter != null)
            {
                Effecter e = Props.icicleEffecter.Spawn(cell, map);
                e.Cleanup();
            }
            else
            {
                EffecterDefOf.ImpactSmallDustCloud.Spawn(cell, map);
            }

            if (Props.icicleFleck != null)
                FleckMaker.Static(cell.ToVector3Shifted(), map, Props.icicleFleck);

            if (Props.icicleMote != null)
            {
                int count = Rand.RangeInclusive(2, 3);
                for (int i = 0; i < count; i++)
                {
                    MoteThrown mote = (MoteThrown)ThingMaker.MakeThing(Props.icicleMote);
                    Vector3 jitter = new Vector3(Rand.Range(-0.4f, 0.4f), 0f, Rand.Range(-0.4f, 0.4f));
                    mote.exactPosition = cell.ToVector3Shifted() + jitter;
                    mote.Scale = Props.moteScale * Rand.Range(0.7f, 1.3f);
                    mote.exactRotation = Rand.Range(0f, 360f);
                    mote.SetVelocity(Rand.Range(0f, 360f), Rand.Range(0.3f, 0.9f));
                    GenSpawn.Spawn(mote, cell, map);
                }
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn pawn = parent.pawn;
            if (pawn?.Map == null) return;

            List<IntVec3> cells = GenRadial
                .RadialCellsAround(target.Cell, Props.radius, true)
                .Where(c => c.InBounds(pawn.Map))
                .ToList();

            GenDraw.DrawFieldEdges(cells, Color.cyan);
        }
    }
}