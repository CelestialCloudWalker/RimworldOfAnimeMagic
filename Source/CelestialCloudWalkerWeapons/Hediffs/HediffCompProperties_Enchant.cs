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

    public class HediffCompProperties_ApplyFaceTattoo : HediffCompProperties
    {
        public TattooDef tattooDef;

        public HediffCompProperties_ApplyFaceTattoo()
        {
            compClass = typeof(ApplyFaceTattoo);
        }
    }

    public class ApplyFaceTattoo : HediffComp
    {
        HediffCompProperties_ApplyFaceTattoo Props => (HediffCompProperties_ApplyFaceTattoo)props;


        private TattooDef OriginalTattoo = null;
    
        public override void CompPostMake()
        {
            Log.Message("CompPostMake running for face tattoo");
            base.CompPostMake();
            if (this.Pawn.style.FaceTattoo != null)
            {
                Log.Message($"Previous tattoo found: {this.Pawn.style.FaceTattoo}");
                OriginalTattoo = this.Pawn.style.FaceTattoo;
            }
            this.Pawn.style.FaceTattoo = Props.tattooDef;
            Log.Message($"Applied new tattoo: {Props.tattooDef}");
            this.Pawn.style.Notify_StyleItemChanged();
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();


            if (OriginalTattoo != null)
            {

                this.Pawn.style.FaceTattoo = OriginalTattoo;
                this.Pawn.style.Notify_StyleItemChanged();
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();

            Scribe_Defs.Look(ref OriginalTattoo, "originalTattoo");
        }
    }
}