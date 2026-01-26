using System;
using Verse;
using RimWorld;

namespace AnimeArsenal
{
    public class AbilityUnlock
    {
        public int pawnsRequired;
        public AbilityDef ability;
        public HediffDef hediff;
        public string unlockMessage = "{0} has unlocked {1}!";
    }
}