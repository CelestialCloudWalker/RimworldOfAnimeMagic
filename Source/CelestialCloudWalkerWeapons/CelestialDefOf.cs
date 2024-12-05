using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace CelestialCloudWalkerWeapons

{
    [DefOf]
    internal class CelestialDefof
    {

        //stats
        public static StatDef AstralPulse;
        public static StatDef AstralPulse_Max;
        public static StatDef AstralPulse_RegenRate;
        public static StatDef AstralPulse_RegenTicks;
        public static StatDef AstralPulse_Cost;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        static CelestialDefof()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(CelestialDefof));
        }
    }
}

