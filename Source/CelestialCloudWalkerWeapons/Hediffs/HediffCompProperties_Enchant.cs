using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace AnimeArsenal
{
    public class HediffCompProperties_Enchant : HediffCompProperties
    {
        public DamageDef damageType;
        public float damageValue;
        public Hediff hediffToApply;

        public HediffCompProperties_Enchant()
        {
            compClass = typeof(EnchantComp);
        }
    }

    public class EnchantComp : HediffComp
    {
        HediffCompProperties_Enchant Props => (HediffCompProperties_Enchant)props;

        public void ApplyEnchant(Pawn TargetPawn)
        {
            if (Props.damageType != null && Props.damageValue > 0)
            {
                TargetPawn.TakeDamage(new DamageInfo(Props.damageType, Props.damageValue));
            }

            if (Props.hediffToApply != null)
            {
                TargetPawn.health.AddHediff(Props.hediffToApply);
            }
        }
    }
}
