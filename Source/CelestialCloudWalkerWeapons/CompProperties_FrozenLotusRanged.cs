using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{

    public class HediffCompProperties_DespawnAfterTicks : HediffCompProperties
    {
        public int despawnAfterTicks = 3600;

        public HediffCompProperties_DespawnAfterTicks()
        {
            compClass = typeof(HediffComp_DespawnAfterTicks);
        }
    }

    public class HediffComp_DespawnAfterTicks : HediffComp
    {
        private int ticksRemaining = -1;

        public new HediffCompProperties_DespawnAfterTicks Props =>
            (HediffCompProperties_DespawnAfterTicks)props;

        public override void CompPostMake()
        {
            base.CompPostMake();
            ticksRemaining = Props.despawnAfterTicks;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            ticksRemaining--;
            if (ticksRemaining > 0) return;

            Pawn pawn = parent.pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned) return;

            pawn.DeSpawn(DestroyMode.Vanish);
            pawn.Destroy(DestroyMode.Vanish);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", -1);
        }
    }


    public class CompProperties_FrozenLotusRanged : CompProperties
    {
        public ThingDef gasDef;
        public float gasRange = 12f;
        public float gasRadius = 2f;
        public int gasSpawnChance_Pct = 70;
        public int gasIntervalTicks = 120;
        public int gasNumCells = 15;
        public IntRange gasLifetimeTicks = new IntRange(600, 900);
        public DamageDef damageDef;
        public IntRange damageAmount = new IntRange(3, 6);
        public int damageIntervalTicks = 60;

        public CompProperties_FrozenLotusRanged()
        {
            compClass = typeof(Comp_FrozenLotusRanged);
        }
    }

    public class Comp_FrozenLotusRanged : ThingComp
    {
        public CompProperties_FrozenLotusRanged Props => (CompProperties_FrozenLotusRanged)props;

        private int ticksUntilNextGas = 0;

        public override void CompTick()
        {
            base.CompTick();

            if (!(parent is Pawn pawn) || !pawn.Spawned || pawn.Dead || pawn.Map == null)
                return;

            ticksUntilNextGas--;
            if (ticksUntilNextGas > 0) return;
            ticksUntilNextGas = Props.gasIntervalTicks;

            Pawn target = FindNearestEnemy(pawn);
            if (target == null) return;

            SprayGasToward(pawn, target.Position, pawn.Map);
        }

        private Pawn FindNearestEnemy(Pawn pawn)
        {
            return pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != pawn
                    && p.Faction != null
                    && p.Faction.HostileTo(pawn.Faction)
                    && !p.Dead
                    && p.Position.DistanceTo(pawn.Position) <= Props.gasRange)
                .OrderBy(p => p.Position.DistanceTo(pawn.Position))
                .FirstOrDefault();
        }

        private void SprayGasToward(Pawn caster, IntVec3 targetPos, Map map)
        {
            if (Props.gasDef == null) return;

            int spawned = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(targetPos, Props.gasRadius, true))
            {
                if (!cell.InBounds(map) || cell.Filled(map)) continue;
                if (spawned >= Props.gasNumCells) break;
                if (!Rand.Chance(Props.gasSpawnChance_Pct / 100f)) continue;
                if (map.thingGrid.ThingAt(cell, Props.gasDef) != null) continue;

                Thing gas = ThingMaker.MakeThing(Props.gasDef);
                GenSpawn.Spawn(gas, cell, map);

                ToxicGasManager gasManager = map.GetComponent<ToxicGasManager>();
                if (gasManager == null)
                {
                    gasManager = new ToxicGasManager(map);
                    map.components.Add(gasManager);
                }

                gasManager.RegisterGasCloudManual(cell, Props, map);
                spawned++;
            }
        }
    }
}