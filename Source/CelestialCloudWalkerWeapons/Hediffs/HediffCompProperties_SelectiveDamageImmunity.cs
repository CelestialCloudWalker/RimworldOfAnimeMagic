using System.Collections.Generic;
using Verse;

namespace AnimeArsenal
{
    public class HediffCompProperties_SelectiveDamageImmunity : HediffCompProperties
    {
        public List<DamageTypeModifiers> vulnerableToDamageTypes;

        public HediffCompProperties_SelectiveDamageImmunity()
        {
            compClass = typeof(HediffComp_SelectiveDamageImmunity);
        }
    }

    public class HediffComp_SelectiveDamageImmunity : HediffComp
    {
        public  HediffCompProperties_SelectiveDamageImmunity Props => (HediffCompProperties_SelectiveDamageImmunity)props;

        public bool IsVulnerableToDamage(DamageDef damageDef)
        {
            return Props.vulnerableToDamageTypes != null &&
                   Props.vulnerableToDamageTypes.Any(x => x.DamageType == damageDef);
        }

        public float GetDamageMod(DamageDef damageType)
        {
            if (Props.vulnerableToDamageTypes == null)
                return 1f;

            var modifier = Props.vulnerableToDamageTypes.FirstOrDefault(x => x.DamageType == damageType);
            return modifier != null ? modifier.DamageModifier : 1f;
        }


        public DamageInfo ModifyDamage(DamageInfo DamageInfo)
        {
            float damageMod = GetDamageMod(DamageInfo.Def);
            DamageInfo.SetAmount(DamageInfo.Amount * damageMod);
            return DamageInfo;
        }
    }

    public class DamageTypeModifiers
    {
        public DamageDef DamageType;
        public float DamageModifier = 1f;
    }
}