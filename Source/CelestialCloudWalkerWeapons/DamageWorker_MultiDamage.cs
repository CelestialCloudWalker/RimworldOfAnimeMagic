using Verse;

namespace AnimeArsenal
{
    public class DamageWorker_MultiDamage : DamageWorker
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            DamageResult primaryResult = base.Apply(dinfo, victim);

            MultiDamageDef multiDef = def as MultiDamageDef;
            if (multiDef?.secondaryDamages == null || victim == null)
                return primaryResult;


            Log.Message(multiDef);


            Log.Message(multiDef.secondaryDamages.Count);

            foreach (DamageData damageData in multiDef.secondaryDamages)
            {
                Log.Message(damageData);

                DamageInfo secondaryDinfo = new DamageInfo(
                    def: damageData.damage,
                    amount: dinfo.Amount * damageData.multiplier,
                    armorPenetration: 1,
                    angle: dinfo.Angle,
                    instigator: dinfo.Instigator,
                    hitPart: dinfo.HitPart,
                    weapon: dinfo.Weapon,
                    category: dinfo.Category
                );

                //secondaryDinfo.SetBodyRegion(dinfo.Height, dinfo.Depth);
                victim.TakeDamage(secondaryDinfo);
            }

            return primaryResult;
        }
    }
}
