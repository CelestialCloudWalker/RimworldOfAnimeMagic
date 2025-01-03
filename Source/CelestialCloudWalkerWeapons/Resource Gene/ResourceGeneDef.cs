﻿using RimWorld;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class ResourceGeneDef : GeneDef
    {
        public string resourceName = "resourceName";
        public StatDef maxStat;
        public StatDef regenTicks;
        public StatDef regenStat;
        public StatDef regenSpeedStat;
        public StatDef costMult;
        public Color barColor;

        public ResourceGeneDef()
        {
            geneClass = typeof(Resource_Gene);
        }
    }
}
