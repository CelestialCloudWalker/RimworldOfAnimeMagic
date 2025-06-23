using UnityEngine;
using Verse;


namespace AnimeArsenal
{
    internal class EmosGraphic_MoteWithAgeSecsCustom : Graphic_MoteWithAgeSecs
    {
        private MaterialPropertyBlock MPB;
        public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
        {
            if (MPB == null)
            {
                MPB = new MaterialPropertyBlock();
            }

            // MPB.SetColor(ShaderPropertyIDs.Color, new Color(Rand.Value, Rand.Value, Rand.Value));

            Graphic_Mote.DrawMote(this.data, this.MatSingle, new Color(Rand.Value, Rand.Value, Rand.Value), loc, rot, thingDef, thing, 0, true, MPB);
        }
    }
}