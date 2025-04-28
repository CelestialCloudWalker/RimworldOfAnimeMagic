using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace AnimeArsenal
{
    public class DashTrailEffect
    {
        private readonly Map map;
        private readonly List<TrackedMotePos> activeTrails = new List<TrackedMotePos>();
        private static readonly int TrailLifetime = 120;
        private static readonly ThingDef TrailMoteDef = DefDatabase<ThingDef>.GetNamed("Mote_GraserBeamBase");

        public DashTrailEffect(Map map)
        {
            this.map = map;
        }

        public void CreateTrailBetween(IntVec3 source, IntVec3 target)
        {
            if (!source.IsValid || !target.IsValid || map == null)
                return;

            if (TrailMoteDef == null)
            {
                Log.Error("DashTrailEffect: Trail mote def not found");
                return;
            }

            MoteDualAttached mote = (MoteDualAttached)ThingMaker.MakeThing(TrailMoteDef);
            if (mote == null) return;

            GenSpawn.Spawn(mote, source, map);
            mote.Attach(new TargetInfo(source, map, false), new TargetInfo(target, map, false));

            TrackedMotePos trackedMote = new TrackedMotePos(mote, source, target, TrailLifetime);
            activeTrails.Add(trackedMote);
        }

        public void Tick()
        {
            for (int i = activeTrails.Count - 1; i >= 0; i--)
            {
                TrackedMotePos trail = activeTrails[i];
                trail.RemainingTicks--;

                if (trail.RemainingTicks <= 0 || trail.Mote == null || trail.Mote.Destroyed)
                {
                    if (trail.Mote != null && !trail.Mote.Destroyed)
                    {
                        trail.Mote.Destroy();
                    }
                    activeTrails.RemoveAt(i);
                }
                else
                {
                    trail.Mote.Maintain();
                }
            }
        }

        public void Clear()
        {
            foreach (TrackedMotePos trail in activeTrails)
            {
                if (trail.Mote != null && !trail.Mote.Destroyed)
                {
                    trail.Mote.Destroy();
                }
            }
            activeTrails.Clear();
        }
    }
}
