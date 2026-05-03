using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_AbilityGasSpray : CompProperties_AbilityEffect
    {
        public ThingDef gasDef;
        public int numCellsToHit = 9;
        public IntRange gasLifetimeTicks = new IntRange(1800, 3600);
        public float gasSpawnChance = 0.8f;
        public float range = 8.0f;
        public float angle = 60.0f;
        public DamageDef damageDef;
        public IntRange damageAmount = new IntRange(3, 6);
        public int damageIntervalTicks = 60;
        public bool affectHostile = true;
        public bool affectNeutral = true;
        public bool affectFriendly = false;
        public bool affectAnimals = true;
        public bool affectMechanoids = false;
        public HediffDef hediffOnExposure;
        public float hediffSeverityPerInterval = 0.05f;
        public EffecterDef spawnEffecter;

        public CompProperties_AbilityGasSpray()
        {
            compClass = typeof(CompAbilityEffect_GasSpray);
        }
    }

    public class CompAbilityEffect_GasSpray : CompAbilityEffect
    {
        public new CompProperties_AbilityGasSpray Props => (CompProperties_AbilityGasSpray)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn pawn = parent.pawn;
            if (pawn?.Map == null) return;

            IntVec3 targetCell = target.IsValid && target.Cell != IntVec3.Invalid
                ? target.Cell
                : pawn.Position;

            Map map = pawn.Map;
            List<IntVec3> cells = GetCellsInCone(pawn.Position, targetCell, map);

            ToxicGasManager gasManager = map.GetComponent<ToxicGasManager>();
            if (gasManager == null)
            {
                gasManager = new ToxicGasManager(map);
                map.components.Add(gasManager);
            }

            int spawned = 0;
            foreach (IntVec3 cell in cells)
            {
                if (spawned >= Props.numCellsToHit) break;
                if (!cell.InBounds(map) || cell.Filled(map)) continue;
                if (!Rand.Chance(Props.gasSpawnChance)) continue;
                if (Props.gasDef != null && map.thingGrid.ThingAt(cell, Props.gasDef) != null) continue;
                if (Props.gasDef != null)
                {
                    Thing gas = ThingMaker.MakeThing(Props.gasDef);
                    GenSpawn.Spawn(gas, cell, map);
                }

                if (Props.spawnEffecter != null)
                {
                    Effecter e = Props.spawnEffecter.Spawn(cell, map);
                    e.Cleanup();
                }

                gasManager.RegisterGasCloud(cell, Props);
                spawned++;
            }
        }

        private List<IntVec3> GetCellsInCone(IntVec3 origin, IntVec3 target, Map map)
        {
            List<IntVec3> result = new List<IntVec3>();

            Vector2 direction = new Vector2(target.x - origin.x, target.z - origin.z);
            bool fullCircle = Props.angle >= 360f || direction.magnitude < 0.01f;

            float halfAngle = Props.angle * 0.5f;
            float rangeSq = Props.range * Props.range;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(origin, Props.range, true))
            {
                if (!cell.InBounds(map)) continue;

                float distSq = (cell - origin).LengthHorizontalSquared;
                if (distSq > rangeSq) continue;

                if (!fullCircle)
                {
                    Vector2 toCell = new Vector2(cell.x - origin.x, cell.z - origin.z);
                    float angleBetween = Vector2.Angle(direction, toCell);
                    if (angleBetween > halfAngle) continue;
                }

                result.Add(cell);
            }

            return result;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn pawn = parent.pawn;
            if (pawn?.Map == null) return;

            IntVec3 targetCell = target.IsValid && target.Cell != IntVec3.Invalid
                ? target.Cell
                : pawn.Position;

            List<IntVec3> cells = GetCellsInCone(pawn.Position, targetCell, pawn.Map);
            GenDraw.DrawFieldEdges(cells, Color.cyan);
        }
    }

    public class ToxicGasManager : MapComponent
    {
        private Dictionary<IntVec3, GasCloudData> activeClouds = new Dictionary<IntVec3, GasCloudData>();

        public ToxicGasManager(Map map) : base(map) { }

        public void RegisterGasCloud(IntVec3 position, CompProperties_AbilityGasSpray props)
        {
            if (activeClouds.ContainsKey(position)) return;

            activeClouds[position] = new GasCloudData
            {
                position = position,
                lifetimeRemaining = props.gasLifetimeTicks.RandomInRange,
                nextDamageTick = GenTicks.TicksGame + props.damageIntervalTicks,
                damageDef = props.damageDef,
                damageAmount = props.damageAmount,
                damageInterval = props.damageIntervalTicks,
                affectHostile = props.affectHostile,
                affectNeutral = props.affectNeutral,
                affectFriendly = props.affectFriendly,
                affectAnimals = props.affectAnimals,
                affectMechanoids = props.affectMechanoids,
                gasDef = props.gasDef,
                hediffOnExposure = props.hediffOnExposure,
                hediffSeverityPerInterval = props.hediffSeverityPerInterval
            };
        }

        public void RegisterGasCloudManual(IntVec3 position, CompProperties_FrozenLotusRanged props, Map map)
        {
            if (activeClouds.ContainsKey(position)) return;

            activeClouds[position] = new GasCloudData
            {
                position = position,
                lifetimeRemaining = props.gasLifetimeTicks.RandomInRange,
                nextDamageTick = GenTicks.TicksGame + props.damageIntervalTicks,
                damageDef = props.damageDef,
                damageAmount = props.damageAmount,
                damageInterval = props.damageIntervalTicks,
                affectHostile = true,
                affectNeutral = true,
                affectFriendly = false,
                affectAnimals = true,
                affectMechanoids = false,
                gasDef = props.gasDef
            };
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            List<IntVec3> toRemove = new List<IntVec3>();

            foreach (var kvp in activeClouds)
            {
                GasCloudData data = kvp.Value;
                data.lifetimeRemaining--;

                if (data.lifetimeRemaining <= 0)
                {
                    if (data.gasDef != null && data.position.InBounds(map))
                    {
                        Thing gasThing = map.thingGrid.ThingAt(data.position, data.gasDef);
                        gasThing?.Destroy();
                    }
                    toRemove.Add(kvp.Key);
                    continue;
                }

                if (GenTicks.TicksGame >= data.nextDamageTick)
                {
                    ApplyEffectsToPawnsAt(data);
                    data.nextDamageTick = GenTicks.TicksGame + data.damageInterval;
                }
            }

            foreach (IntVec3 cell in toRemove)
                activeClouds.Remove(cell);
        }

        private void ApplyEffectsToPawnsAt(GasCloudData data)
        {
            if (!data.position.InBounds(map)) return;

            foreach (Thing thing in data.position.GetThingList(map).ToList())
            {
                if (!(thing is Pawn pawn) || pawn.Dead) continue;

                bool isHostile = pawn.Faction == null || pawn.Faction.HostileTo(Faction.OfPlayer);
                bool isFriendly = pawn.Faction != null && !pawn.Faction.HostileTo(Faction.OfPlayer);
                bool isAnimal = pawn.RaceProps.Animal;
                bool isMechanoid = pawn.RaceProps.IsMechanoid;

                if (isAnimal && !data.affectAnimals) continue;
                if (isMechanoid && !data.affectMechanoids) continue;
                if (isHostile && !data.affectHostile) continue;
                if (isFriendly && !data.affectFriendly) continue;

                if (data.damageDef != null)
                {
                    int dmg = data.damageAmount.RandomInRange;
                    DamageInfo dinfo = new DamageInfo(data.damageDef, dmg, 0f, -1f, null);
                    pawn.TakeDamage(dinfo);
                }

                if (data.hediffOnExposure != null && data.hediffSeverityPerInterval > 0f)
                {
                    Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(data.hediffOnExposure);
                    if (existing != null)
                        existing.Severity += data.hediffSeverityPerInterval;
                    else
                    {
                        Hediff newHediff = HediffMaker.MakeHediff(data.hediffOnExposure, pawn);
                        newHediff.Severity = data.hediffSeverityPerInterval;
                        pawn.health.AddHediff(newHediff);
                    }
                }
            }
        }
    }

    public class GasCloudData
    {
        public IntVec3 position;
        public int lifetimeRemaining;
        public int nextDamageTick;
        public DamageDef damageDef;
        public IntRange damageAmount;
        public int damageInterval;
        public bool affectHostile;
        public bool affectNeutral;
        public bool affectFriendly;
        public bool affectAnimals;
        public bool affectMechanoids;
        public ThingDef gasDef;
        public HediffDef hediffOnExposure;
        public float hediffSeverityPerInterval;
    }
}