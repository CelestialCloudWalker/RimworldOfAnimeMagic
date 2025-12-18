using System;
using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class ForceXenotypeExtension : DefModExtension
    {
        public XenotypeDef forcedXenotype;
        public bool onlyIfNoXenotype = false; 
        public bool keepExistingGenes = true; 
    }

    public class Gene_ForceXenotype : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            ApplyForcedXenotype();
        }

        private void ApplyForcedXenotype()
        {
            if (pawn?.genes == null) return;

            var ext = def.GetModExtension<ForceXenotypeExtension>();
            if (ext?.forcedXenotype == null) return;

            if (ext.onlyIfNoXenotype && pawn.genes.Xenotype != XenotypeDefOf.Baseliner)
            {
                return;
            }

            if (ext.keepExistingGenes)
            {
                pawn.genes.xenotypeName = ext.forcedXenotype.label;
                pawn.genes.hybrid = false;
            }
            else
            {
                pawn.genes.SetXenotype(ext.forcedXenotype);
            }

            Log.Message($"[ForceXenotype] Applied xenotype {ext.forcedXenotype.defName} to {pawn.Name} (keepGenes: {ext.keepExistingGenes})");
        }
    }
}