using JetBrains.Annotations;
using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AnimeArsenal
{
    public static class AnimeArsenalUtility
    {
        public static void CreateLightningStrike(Map Map, IntVec3 Position, Thing Instigator, DamageDef DamageDef, int DamageAmount = 10, float Arp = -1, float radius = 1f)
        {
            WeatherEvent_LightningStrike lightningStrike = new WeatherEvent_LightningStrike(Map, Position);
            lightningStrike.FireEvent();

            // Apply additional effects
            if (radius > 0f)
            {
                GenExplosion.DoExplosion(
                    Position,
                    Map,
                    radius,
                    DamageDef,
                    Instigator,
                    DamageAmount,
                    Arp
                );
            }
        }


        public static int TicksInSeconds(int Seconds)
        {
            return 60 * Seconds;
        }

        public static IEnumerable<HediffComp_SelectiveDamageImmunity> GetSelectiveDamageImmunityComps(this Pawn pawn)
        {
            return pawn.health.hediffSet.GetAllComps()
                .OfType<HediffComp_SelectiveDamageImmunity>();
        }

        public static bool HasSelectiveDamageImmunity(this Pawn pawn)
        {
            return pawn.GetSelectiveDamageImmunityComps().Any();
        }

        public static void TrainPawn(Pawn PawnToTrain, Pawn Trainer = null)
        {
            if (PawnToTrain.training != null)
            {
                foreach (var item in DefDatabase<TrainableDef>.AllDefsListForReading)
                {
                    if (PawnToTrain.training.CanAssignToTrain(item).Accepted)
                    {
                        PawnToTrain.training.SetWantedRecursive(item, true);
                        PawnToTrain.training.Train(item, Trainer, true);
                    }

                }
            }
        }
        public static void DealDamageToThingsInRange(List<Thing> ThingsInRadius, DamageDef DamageDef, float Damage, float ArmourPen = 0, float Angle = -1f, Thing Instigator = null, EffecterDef EffectorToPlay = null, Func<Thing, bool> Predicate = null)
        {
            foreach (var item in ThingsInRadius)
            {
                if (!item.Destroyed)
                {
                    if (EffectorToPlay != null)
                    {
                        EffectorToPlay.SpawnMaintained(item.Position, item.Map);
                    }
                    item.TakeDamage(new DamageInfo(DamageDef, Damage, ArmourPen, Angle, Instigator));
                }
            }
        }


        public static void DealDamageToThingsInRange(IntVec3 center, Map map, float radius, DamageDef DamageDef, float Damage, float ArmourPen = 0, float Angle = -1f, Thing Instigator = null, EffecterDef EffectorToPlay = null, Func<Thing, bool> Predicate = null)
        {
            List<Thing> ThingsInRadius = AnimeArsenalUtility.GetThingsInRange(center, map, radius).ToList();
            DealDamageToThingsInRange(ThingsInRadius, DamageDef, Damage, ArmourPen, Angle, Instigator, EffectorToPlay, Predicate);
        }


        public static IEnumerable<Thing> GetThingsInRange(IntVec3 center, Map map, float radius, Func<Thing, bool> Predicate = null)
        {
            return GenRadial.RadialCellsAround(center, radius, true)
                .SelectMany(c => c.GetThingList(map))
                .OfType<Thing>()
                .Where(p => Predicate == null || Predicate(p));
        }
        public static IEnumerable<Pawn> GetEnemyPawnsInRange(IntVec3 center, Map map, float radius)
        {
            return GenRadial.RadialCellsAround(center, radius, true)
                .SelectMany(c => c.GetThingList(map))
                .OfType<Pawn>()
                .Where(p => p.Faction != null && p.Faction != Faction.OfPlayer);
        }

    }
}
