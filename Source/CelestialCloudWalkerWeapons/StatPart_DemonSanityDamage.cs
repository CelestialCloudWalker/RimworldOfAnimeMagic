using RimWorld;
using System.Linq;
using Verse;

namespace AnimeArsenal
{
    public class StatPart_DemonSanityDamage : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!req.HasThing || !(req.Thing is Pawn pawn))
                return;

            var demonGene = pawn.genes?.GenesListForReading
                .FirstOrDefault(g => g is BloodDemonArtsGene && g.Active) as BloodDemonArtsGene;

            if (demonGene == null)
                return;

            float multiplier = demonGene.DamageMultiplier;
            val *= multiplier;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!req.HasThing || !(req.Thing is Pawn pawn))
                return null;

            var demonGene = pawn.genes?.GenesListForReading
                .FirstOrDefault(g => g is BloodDemonArtsGene && g.Active) as BloodDemonArtsGene;

            if (demonGene == null)
                return null;

            float multiplier = demonGene.DamageMultiplier;
            float sanityPercent = demonGene.SanityPercent;

            return $"Demon sanity ({sanityPercent:P0}): x{multiplier:F2}";
        }
    }
}