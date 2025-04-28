using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class DashTrailManager
    {
        private List<TrackedMote> activeEffects = new List<TrackedMote>();
        private static readonly int MoteLifetime = 120;
        private static readonly ThingDef TrailMoteDef = DefDatabase<ThingDef>.GetNamed("MagicAndMyths_DashTrailMote");

        public void CreateTrailBetween(Thing source, Thing target, Map map)
        {
            if (source == null || target == null || !source.Spawned || !target.Spawned || map == null)
                return;

            MoteDualAttached mote = MoteMaker.MakeInteractionOverlay(
                TrailMoteDef,
                source,
                new TargetInfo(target.Position, map, false));

            if (mote == null)
                return;

            TrackedMote trackedMote = new TrackedMote(mote, source, target, MoteLifetime);
            activeEffects.Add(trackedMote);
        }

        public void Tick()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                TrackedMote trackedMote = activeEffects[i];
                trackedMote.RemainingTicks--;

                if (trackedMote.RemainingTicks <= 0 ||
                    trackedMote.Mote == null ||
                    trackedMote.Mote.Destroyed ||
                    trackedMote.SourceThing == null ||
                    !trackedMote.SourceThing.Spawned ||
                    trackedMote.TargetThing == null ||
                    !trackedMote.TargetThing.Spawned)
                {
                    if (trackedMote.Mote != null && !trackedMote.Mote.Destroyed)
                        trackedMote.Mote.Destroy();
                    activeEffects.RemoveAt(i);
                }
                else
                {
                    trackedMote.Mote.Maintain();
                    trackedMote.Mote.UpdateTargets(
                        trackedMote.SourceThing,
                        trackedMote.TargetThing,
                        Vector3.zero,
                        Vector3.zero
                    );
                }
            }
        }

        public void Clear()
        {
            foreach (TrackedMote trackedMote in activeEffects)
            {
                if (trackedMote.Mote != null && !trackedMote.Mote.Destroyed)
                    trackedMote.Mote.Destroy();
            }
            activeEffects.Clear();
        }
    }
}