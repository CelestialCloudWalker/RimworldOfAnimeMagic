using System.Collections.Generic;
using Verse;

namespace AnimeArsenal
{
    public class TrainingItemExtension : DefModExtension
    {
        public string capacityToBoost;  
        public string statToBoost;      

        public float increaseAmount = 0.05f;

        public float successChance = 0.7f;

        public List<string> failureBodyParts = new List<string>();

        public float failureDamage = 15f;

        public int trainingTime = 2500;

        public int cooldownTime = 60000; 
    }
}