using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_BeamEffect : CompProperties
    {
        public ThingDef TrailMote;
        public float MoteInterval = 0.1f;
        public float MoteWidth = 1f;
        public float MoteDuration = 60f;
        public bool AlignWithMovement = true;

        public CompProperties_BeamEffect()
        {
            compClass = typeof(Comp_BeamEffect);
        }
    }

    public class Comp_BeamEffect : ThingComp
    {
        private CompProperties_BeamEffect Props => (CompProperties_BeamEffect)props;
        private int ticksSinceLastMote = 0;
        private Mote currentMote;

        public override void CompTick()
        {
            base.CompTick();
            if (parent.Map == null || Props.TrailMote == null) return;

            ticksSinceLastMote++;
            if (ticksSinceLastMote >= Props.MoteInterval * 60)
            {
                SpawnScaledMote();
                ticksSinceLastMote = 0;
            }
        }

        private void SpawnScaledMote()
        {
            if (!(parent is Projectile projectile)) return;

            if (currentMote == null)
            {
                Mote mote = (Mote)ThingMaker.MakeThing(Props.TrailMote, null);
                IntVec3 launcherCell = projectile.Launcher.Position;
                Vector3 launcherPos = projectile.Launcher.DrawPos;
                IntVec3 targetCell = projectile.intendedTarget.Cell;
                Vector3 direction = (targetCell.ToVector3() - launcherPos).normalized;

                float distance = (targetCell - launcherCell).LengthHorizontal;
                mote.exactPosition = launcherPos + (direction * (distance / 2f));

                currentMote = (Mote)GenSpawn.Spawn(mote, launcherCell, parent.Map);
                currentMote.exactRotation = direction.ToAngleFlat();

                currentMote.Scale = distance;
            }
        }
    }
}