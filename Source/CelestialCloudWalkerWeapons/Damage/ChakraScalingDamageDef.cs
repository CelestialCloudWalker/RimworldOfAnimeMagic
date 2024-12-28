using Verse;

namespace AnimeArsenal.ChakraScalingDamageDef
{
    public class ChakraScalingDamageDef : DamageDef
    {
        public float damageMultiplier = 2f;
    }

    public class DamageWorker_ChakraScaling : DamageWorker
    {
        ChakraScalingDamageDef ChakraScalingDamageDef => def as ChakraScalingDamageDef;

        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            DamageResult primaryResult = base.Apply(dinfo, victim);

            if (dinfo.Instigator != null && dinfo.Instigator is Pawn pawn)
            {
                // Checks if the pawn has any genes from the Naruto GeneCategoryDef
                if (HasChakra(pawn))
                {
                    // Double the damage by multiplying the old Amount by 2 then set it again
                    dinfo.SetAmount(dinfo.Amount * ChakraScalingDamageDef.damageMultiplier);
                }
            }

            return primaryResult;
        }

        private bool HasChakra(Pawn pawn)
        {
            return pawn.genes.GenesListForReading.Any(x => x.def.displayCategory == DefDatabase<GeneCategoryDef>.GetNamed("WN_Naruto"));
        }
    }
}
