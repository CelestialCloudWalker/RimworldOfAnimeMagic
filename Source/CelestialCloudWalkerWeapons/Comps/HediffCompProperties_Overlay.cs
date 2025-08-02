using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class HediffCompProperties_Overlay : HediffCompProperties
    {
        public bool overlayPawn = false;
        public GraphicData overlayGraphic;
        public bool useMaskTexture = true;
        public string customMaskPath = "";
        public AltitudeLayer altitudeLayer = AltitudeLayer.MoteOverhead;

        public HediffCompProperties_Overlay()
        {
            compClass = typeof(HediffComp_Overlay);
        }
    }

    public class HediffComp_Overlay : HediffComp
    {
        private Texture2D _maskTex;
        public bool showOverlay = true;

        public HediffCompProperties_Overlay Props => (HediffCompProperties_Overlay)props;

        public Texture2D MaskTex
        {
            get
            {
                if (_maskTex == null)
                {
                    if (!string.IsNullOrEmpty(Props.customMaskPath))
                    {
                        _maskTex = ContentFinder<Texture2D>.Get(Props.customMaskPath);
                    }
                    else
                    {
                        _maskTex = ContentFinder<Texture2D>.Get(this.Pawn.equipment.Primary.def.graphicData.texPath);
                    }

                }
                return _maskTex;
            }
        }
    }
}