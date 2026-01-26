using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnimeArsenal
{
    public class DemonStateTransfer
    {
        public int totalPawnsEaten;
        public float currentBloodValue;
        public DemonRank currentRank;
        public float lastKnownMax;
        public bool hasSpecialized;
        public HashSet<int> unlockedAbilityIndices;

        public DemonStateTransfer(BloodDemonArtsGene sourceGene)
        {
            totalPawnsEaten = sourceGene.TotalPawnsEaten;
            currentBloodValue = sourceGene.Value;
            currentRank = sourceGene.CurrentRank;
            lastKnownMax = sourceGene.lastKnownMax;
            hasSpecialized = sourceGene.HasSpecialized;
            unlockedAbilityIndices = new HashSet<int>(sourceGene.GetUnlockedAbilityIndices());
        }

        public void ApplyTo(BloodDemonArtsGene targetGene)
        {
            targetGene.SetPawnsEaten(totalPawnsEaten);
            targetGene.SetRank(currentRank);
            targetGene.Value = currentBloodValue;
            targetGene.SetSpecialized(hasSpecialized);
            targetGene.SetUnlockedAbilities(unlockedAbilityIndices);
            targetGene.ForceResourceSync();
        }
    }
}
