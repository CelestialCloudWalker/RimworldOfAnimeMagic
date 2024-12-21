using RimWorld;
using System.Collections.Generic;
using UnityEngine;



namespace AnimeArsenal
{
    public class GeneGizmoBreath : GeneGizmo_ResourceAstral
    {
        private BreathingTechniqueGene BreathingTechniqueGene => (BreathingTechniqueGene)gene;

        protected override float Target { get => BreathingTechniqueGene != null ? BreathingTechniqueGene.ExhaustionProgress : base.Target; set => base.Target = value; }


        public GeneGizmoBreath(BreathingTechniqueGene gene, List<IGeneResourceDrain> drainGenes, Color barColor, Color barHighlightColor) : base(gene, drainGenes, barColor, barHighlightColor)
        {

        }
    }
}
