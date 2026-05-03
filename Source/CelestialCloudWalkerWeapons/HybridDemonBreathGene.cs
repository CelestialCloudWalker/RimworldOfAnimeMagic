using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class HybridDemonBreathGene : BloodDemonArtsGene
    {
        private int totalKills = 0;
        private int totalDemonKills = 0;

        public int TotalKills => totalKills;
        public int TotalDemonKills => totalDemonKills;

        public void SetKillCounts(int kills, int demonKills)
        {
            totalKills = kills;
            totalDemonKills = demonKills;
        }

        public void NotifyKill(Pawn victim)
        {
            if (victim == null) return;

            totalKills++;

            bool isDemon = victim.genes?.GenesListForReading
                .Any(g => g.def is BloodDemonArtsGeneDef) ?? false;

            if (isDemon)
                totalDemonKills++;

            if (isDemon)
                Value = UnityEngine.Mathf.Min(Value + 15f, Max);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref totalKills, "hybrid_totalKills", 0);
            Scribe_Values.Look(ref totalDemonKills, "hybrid_totalDemonKills", 0);
        }
    }
}