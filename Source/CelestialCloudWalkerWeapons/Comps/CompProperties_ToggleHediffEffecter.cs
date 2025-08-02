using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_Effecter : CompProperties
    {
        public EffecterDef effecterDef;
        public bool attached = true;

        public CompProperties_Effecter()
        {
            compClass = typeof(Comp_Effecter);
        }
    }




    public class Comp_Effecter : ThingComp
    {
        private Effecter effecter;

        public CompProperties_Effecter Props => (CompProperties_Effecter)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (Props.effecterDef != null)
            {
                effecter = Props.effecterDef.Spawn();
                EffectTick();
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            EffectTick();
        }

        private void EffectTick()
        {
            if (effecter != null && parent.Spawned)
            {
                effecter.EffectTick(parent, parent);
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            base.PostDeSpawn(map);

            if (effecter != null)
            {
                effecter.Cleanup();
                effecter = null;
            }
        }
    }
}