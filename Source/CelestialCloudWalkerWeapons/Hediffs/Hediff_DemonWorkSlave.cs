using Verse;

namespace AnimeArsenal
{
    public class Hediff_DemonWorkSlave : HediffWithComps
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            HediffComp_DemonWorkSlaveEffect zombieComp = this.TryGetComp<HediffComp_DemonWorkSlaveEffect>();
            zombieComp?.CompPostMake();

        }
    }
}