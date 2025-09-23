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
        public static void CreateLightningStrike(Map map, IntVec3 pos, Thing source, DamageDef dmg, int amt = 10, float pen = -1, float rad = 1f)
        {
            var strike = new WeatherEvent_LightningStrike(map, pos);
            strike.FireEvent();

            if (rad > 0f)
            {
                GenExplosion.DoExplosion(pos, map, rad, dmg, source, amt, pen);
            }
        }

        public static int TicksInSeconds(int secs) => 60 * secs;

        public static IEnumerable<HediffComp_SelectiveDamageImmunity> GetSelectiveDamageImmunityComps(this Pawn pawn)
        {
            return pawn.health.hediffSet.GetAllComps().OfType<HediffComp_SelectiveDamageImmunity>();
        }

        public static bool HasSelectiveDamageImmunity(this Pawn pawn) => pawn.GetSelectiveDamageImmunityComps().Any();

        public static void TrainPawn(Pawn target, Pawn trainer = null)
        {
            if (target.training == null) return;

            foreach (var def in DefDatabase<TrainableDef>.AllDefsListForReading)
            {
                if (!target.training.CanAssignToTrain(def).Accepted) continue;
                target.training.SetWantedRecursive(def, true);
                target.training.Train(def, trainer, true);
            }
        }

        public static void DealDamageToThingsInRange(List<Thing> targets, DamageDef dmgDef, float dmg, float pen = 0, float angle = -1f, Thing source = null, EffecterDef fx = null, Func<Thing, bool> filter = null)
        {
            foreach (var thing in targets)
            {
                if (thing.Destroyed) continue;

                if (fx != null)
                    fx.SpawnMaintained(thing.Position, thing.Map);

                thing.TakeDamage(new DamageInfo(dmgDef, dmg, pen, angle, source));
            }
        }

        public static void DealDamageToThingsInRange(IntVec3 center, Map map, float radius, DamageDef dmgDef, float dmg, float pen = 0, float angle = -1f, Thing source = null, EffecterDef fx = null, Func<Thing, bool> filter = null)
        {
            var things = GetThingsInRange(center, map, radius).ToList();
            DealDamageToThingsInRange(things, dmgDef, dmg, pen, angle, source, fx, filter);
        }

        public static IEnumerable<Thing> GetThingsInRange(IntVec3 center, Map map, float radius, Func<Thing, bool> filter = null)
        {
            return GenRadial.RadialCellsAround(center, radius, true)
                .SelectMany(c => c.GetThingList(map))
                .OfType<Thing>()
                .Where(p => filter?.Invoke(p) ?? true);
        }

        public static IEnumerable<Pawn> GetEnemyPawnsInRange(IntVec3 center, Map map, float radius)
        {
            return GenRadial.RadialCellsAround(center, radius, true)
                .SelectMany(c => c.GetThingList(map))
                .OfType<Pawn>()
                .Where(p => p.Faction != null && p.Faction != Faction.OfPlayer);
        }

        public static BodyPartRecord GetRandomLimb(Pawn pawn)
        {
            var limbs = pawn.health.hediffSet.GetNotMissingParts()
                .Where(part => part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore))
                .ToList();

            return limbs.RandomElementWithFallback();
        }

        public static float CalcAstralPulseScalingFactor(Pawn caster, Pawn target, float min = 0.5f, float max = 1.5f)
        {
            float casterPulse = caster.GetStatValue(AnimeArsenal.CelestialDefof.AstralPulse);
            float targetPulse = target.GetStatValue(AnimeArsenal.CelestialDefof.AstralPulse);

            return Mathf.Lerp(min, max, casterPulse / targetPulse);
        }

        public static BodyPartRecord GetRandomPartByTagDef(Pawn pawn, List<BodyPartTagDef> tags)
        {
            var parts = pawn.health.hediffSet.GetNotMissingParts()
                .Where(part => part.def.tags.Any(x => tags.Contains(x)))
                .ToList();
            return parts.RandomElementWithFallback();
        }
    }

}