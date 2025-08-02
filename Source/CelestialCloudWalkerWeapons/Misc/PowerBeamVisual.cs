using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class PowerBeamVisual
    {
        public ThingDef BeamMoteDef { get; private set; }
        public EffecterDef StartBeamMoteDef { get; private set; }
        public EffecterDef EndBeamMoteDef { get; private set; }
        public IntVec3 SourcePosition { get; private set; }
        public Map CurrentMap { get; private set; }
        private MoteBeam beamMote;
        private Effecter endMote;
        private Effecter startMote;
        public int LifetimeTicks { get; private set; } = 0;
        public int TicksUntilDestroy { get; private set; }
        public bool IsRunning { get; private set; } = false;
        public float BeamWidth = 15f;

        public PowerBeamVisual(ThingDef beamMoteDef, EffecterDef endBeamMoteDef, IntVec3 sourcePosition, Map map, float beamWidth, int lifeTicks)
        {
            StartBeamMoteDef = endBeamMoteDef;
            EndBeamMoteDef = endBeamMoteDef;
            BeamMoteDef = beamMoteDef;
            TicksUntilDestroy = lifeTicks;
            SourcePosition = sourcePosition;
            CurrentMap = map;
            BeamWidth = beamWidth;
        }

        public void InitializeBeam(IntVec3 sourcePosition, IntVec3 targetPosition)
        {

            if (endMote == null && EndBeamMoteDef != null)
            {
                endMote = EndBeamMoteDef.SpawnMaintained(targetPosition, CurrentMap);
            }

            if (beamMote == null)
            {
                beamMote = (MoteBeam)ThingMaker.MakeThing(BeamMoteDef, null);
                beamMote = (MoteBeam)GenSpawn.Spawn(beamMote, sourcePosition, CurrentMap);
            }


            SourcePosition = sourcePosition;
            IsRunning = true;
        }

        public void TickBeam(IntVec3 targetPosition)
        {
            if (!IsRunning || beamMote == null) return;

            Vector3 sourcePos = SourcePosition.ToVector3Shifted();
            Vector3 targetPos = targetPosition.ToVector3Shifted();
            beamMote.UpdateTargets(sourcePos, targetPos, GetBeamSizeForLifeTime());
            beamMote.Maintain();

            if (endMote != null)
            {
                endMote.EffectTick(new TargetInfo(targetPosition, CurrentMap), new TargetInfo(targetPosition, CurrentMap));
            }

            LifetimeTicks++;
            if (LifetimeTicks >= TicksUntilDestroy)
            {
                DestroyBeam();
            }
        }

        private float GetBeamSizeForLifeTime()
        {
            return Mathf.Lerp(0, BeamWidth, (float)LifetimeTicks / TicksUntilDestroy);
        }

        private void DestroyBeam()
        {
            if (beamMote != null && !beamMote.Destroyed)
            {
                beamMote.Destroy();
                beamMote = null;
            }
            if (endMote != null)
            {
                endMote.Cleanup();
                endMote = null;
            }
            IsRunning = false;
        }
    }
}