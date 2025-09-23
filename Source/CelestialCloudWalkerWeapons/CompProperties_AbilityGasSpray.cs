using System.Collections.Generic;
using System.Linq;
using RimWorld;
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

            if (Props.gasDef == null) return;

            Map map = parent.pawn.Map;
            IntVec3 targetCell = target.Cell;

            List<IntVec3> affectedCells = GetCellsInCone(parent.pawn.Position, targetCell, map, Props.numCellsToHit, Props.range, Props.angle);

            foreach (IntVec3 cell in affectedCells)
            {
                if (Rand.Chance(Props.gasSpawnChance))
                {
                    SpawnGasAt(cell, map);
                }
            }
        }

        private void SpawnGasAt(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map) || cell.Filled(map))
                return;

            if (map.thingGrid.ThingAt(cell, Props.gasDef) != null)
                return;

            Thing gas = ThingMaker.MakeThing(Props.gasDef);

            if (gas is Gas gasInstance)
            {
                var densityField = typeof(Gas).GetField("density", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                densityField?.SetValue(gasInstance, 1.0f);
            }

            GenSpawn.Spawn(gas, cell, map);

            ToxicGasManager gasManager = map.GetComponent<ToxicGasManager>();
            if (gasManager == null)
            {
                gasManager = new ToxicGasManager(map);
                map.components.Add(gasManager);
            }

            gasManager.RegisterGasCloud(cell, Props);
        }

        private List<IntVec3> GetCellsInCone(IntVec3 start, IntVec3 target, Map map, int maxCells, float range, float coneAngle)
        {
            List<IntVec3> cells = new List<IntVec3>();
            Vector3 startVec = start.ToVector3Shifted();
            Vector3 targetVec = target.ToVector3Shifted();
            Vector3 direction = (targetVec - startVec).normalized;

            float halfAngleRad = (coneAngle * 0.5f) * Mathf.Deg2Rad;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(start, range, true))
            {
                if (!cell.InBounds(map) || cell == start)
                    continue;

                Vector3 cellVec = cell.ToVector3Shifted();
                Vector3 toCell = (cellVec - startVec).normalized;

                float angleToCell = Vector3.Angle(direction, toCell) * Mathf.Deg2Rad;

                if (angleToCell <= halfAngleRad)
                {
                    float distance = Vector3.Distance(startVec, cellVec);
                    if (distance <= range)
                    {
                        cells.Add(cell);
                        if (cells.Count >= maxCells)
                            break;
                    }
                }
            }

            return cells;
        }
    }

    public class ToxicGasManager : MapComponent
    {
        private Dictionary<IntVec3, GasCloudData> activeClouds = new Dictionary<IntVec3, GasCloudData>();

        public ToxicGasManager(Map map) : base(map) { }

        public int GetActiveCloudCount() => activeClouds.Count;

        public void RegisterGasCloud(IntVec3 position, CompProperties_AbilityGasSpray props)
        {
            if (!activeClouds.ContainsKey(position))
            {
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
                    gasDef = props.gasDef
                };
            }
        }

        public override void MapComponentTick()
        {
            if (activeClouds.Count == 0) return;

            List<IntVec3> toRemove = new List<IntVec3>();
            int currentTick = GenTicks.TicksGame;

            foreach (var kvp in activeClouds)
            {
                GasCloudData data = kvp.Value;

                Thing gas = map.thingGrid.ThingAt(data.position, data.gasDef);
                if (gas == null)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                data.lifetimeRemaining--;

                if (data.lifetimeRemaining <= 0)
                {
                    gas.Destroy();
                    toRemove.Add(kvp.Key);
                    continue;
                }

                if (currentTick >= data.nextDamageTick)
                {
                    DamagePawnsAt(data);
                    data.nextDamageTick = currentTick + data.damageInterval;
                }
            }

            foreach (IntVec3 pos in toRemove)
                activeClouds.Remove(pos);
        }

        private void DamagePawnsAt(GasCloudData data)
        {
            foreach (Thing thing in map.thingGrid.ThingsListAtFast(data.position).ToList())
            {
                if (!(thing is Pawn pawn) || pawn.Dead || !ShouldAffectPawn(pawn, data))
                    continue;

                int damage = data.damageAmount.RandomInRange;
                DamageInfo damageInfo = new DamageInfo(data.damageDef, damage, 0f, -1f, null, null, null,
                                                     DamageInfo.SourceCategory.ThingOrUnknown);

                pawn.TakeDamage(damageInfo);

                if (PawnUtility.ShouldSendNotificationAbout(pawn))
                    MoteMaker.ThrowText(pawn.DrawPos, map, damage.ToString(), Color.red, 1.9f);
            }
        }

        private bool ShouldAffectPawn(Pawn pawn, GasCloudData data)
        {
            if (pawn.RaceProps.IsMechanoid)
                return data.affectMechanoids;

            if (!pawn.RaceProps.Humanlike)
                return data.affectAnimals;

            if (pawn.Faction == null)
                return data.affectNeutral;

            if (pawn.Faction.HostileTo(Faction.OfPlayer))
                return data.affectHostile;

            if (pawn.Faction == Faction.OfPlayer)
                return data.affectFriendly;

            return data.affectNeutral;
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                List<GasCloudData> cloudList = activeClouds.Values.ToList();
                Scribe_Collections.Look(ref cloudList, "activeClouds", LookMode.Deep);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<GasCloudData> cloudList = new List<GasCloudData>();
                Scribe_Collections.Look(ref cloudList, "activeClouds", LookMode.Deep);

                activeClouds.Clear();
                if (cloudList != null)
                {
                    foreach (GasCloudData data in cloudList)
                        activeClouds[data.position] = data;
                }
            }
        }
    }

    public class GasCloudData : IExposable
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

        public void ExposeData()
        {
            Scribe_Values.Look(ref position, "position");
            Scribe_Values.Look(ref lifetimeRemaining, "lifetimeRemaining");
            Scribe_Values.Look(ref nextDamageTick, "nextDamageTick");
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_Values.Look(ref damageInterval, "damageInterval");
            Scribe_Values.Look(ref affectHostile, "affectHostile");
            Scribe_Values.Look(ref affectNeutral, "affectNeutral");
            Scribe_Values.Look(ref affectFriendly, "affectFriendly");
            Scribe_Values.Look(ref affectAnimals, "affectAnimals");
            Scribe_Values.Look(ref affectMechanoids, "affectMechanoids");
            Scribe_Defs.Look(ref gasDef, "gasDef");

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                int min = damageAmount.min;
                int max = damageAmount.max;
                Scribe_Values.Look(ref min, "damageMin");
                Scribe_Values.Look(ref max, "damageMax");
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                int min = 3, max = 6;
                Scribe_Values.Look(ref min, "damageMin");
                Scribe_Values.Look(ref max, "damageMax");
                damageAmount = new IntRange(min, max);
            }
        }
    }
}