using System.Collections.Generic;
using Verse;
using RimWorld;

namespace AnimeArsenal
{
    public class TrainingComp : ThingComp
    {
        private Dictionary<string, float> capacityBoosts = new Dictionary<string, float>();

        private Dictionary<string, float> statBoosts = new Dictionary<string, float>();

        private Dictionary<string, int> cooldowns = new Dictionary<string, int>();

        public Pawn Pawn => parent as Pawn;

        public void AddCapacityBoost(string capacityDefName, float amount)
        {
            if (capacityBoosts.ContainsKey(capacityDefName))
            {
                capacityBoosts[capacityDefName] += amount;
            }
            else
            {
                capacityBoosts[capacityDefName] = amount;
            }
        }

        public void AddStatBoost(string statDefName, float amount)
        {
            if (statBoosts.ContainsKey(statDefName))
            {
                statBoosts[statDefName] += amount;
            }
            else
            {
                statBoosts[statDefName] = amount;
            }

            if (Pawn != null)
            {
                Pawn.health.capacities.Notify_CapacityLevelsDirty();
                Pawn.skills?.Notify_SkillDisablesChanged();
            }
        }

        public float GetCapacityBoost(string capacityDefName)
        {
            if (capacityBoosts.TryGetValue(capacityDefName, out float boost))
            {
                return boost;
            }
            return 0f;
        }

        public float GetStatBoost(string statDefName)
        {
            if (statBoosts.TryGetValue(statDefName, out float boost))
            {
                return boost;
            }
            return 0f;
        }

        public void SetCooldown(string trainingItemDefName, int cooldownTicks)
        {
            int expirationTick = Find.TickManager.TicksGame + cooldownTicks;
            cooldowns[trainingItemDefName] = expirationTick;
        }

        public bool IsOnCooldown(string trainingItemDefName, out int ticksRemaining)
        {
            if (cooldowns.TryGetValue(trainingItemDefName, out int expirationTick))
            {
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick < expirationTick)
                {
                    ticksRemaining = expirationTick - currentTick;
                    return true;
                }
                else
                {
                    cooldowns.Remove(trainingItemDefName);
                }
            }
            ticksRemaining = 0;
            return false;
        }

        public string GetCooldownString(string trainingItemDefName)
        {
            if (IsOnCooldown(trainingItemDefName, out int ticksRemaining))
            {
                return ticksRemaining.ToStringTicksToPeriod();
            }
            return "Ready";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            List<string> capacityKeys = null;
            List<float> capacityValues = null;

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                capacityKeys = new List<string>(capacityBoosts.Keys);
                capacityValues = new List<float>(capacityBoosts.Values);
            }

            Scribe_Collections.Look(ref capacityKeys, "capacityBoostKeys", LookMode.Value);
            Scribe_Collections.Look(ref capacityValues, "capacityBoostValues", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                capacityBoosts = new Dictionary<string, float>();
                if (capacityKeys != null && capacityValues != null)
                {
                    for (int i = 0; i < capacityKeys.Count; i++)
                    {
                        capacityBoosts[capacityKeys[i]] = capacityValues[i];
                    }
                }
            }

            List<string> statKeys = null;
            List<float> statValues = null;

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                statKeys = new List<string>(statBoosts.Keys);
                statValues = new List<float>(statBoosts.Values);
            }

            Scribe_Collections.Look(ref statKeys, "statBoostKeys", LookMode.Value);
            Scribe_Collections.Look(ref statValues, "statBoostValues", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                statBoosts = new Dictionary<string, float>();
                if (statKeys != null && statValues != null)
                {
                    for (int i = 0; i < statKeys.Count; i++)
                    {
                        statBoosts[statKeys[i]] = statValues[i];
                    }
                }
            }

            List<string> cooldownKeys = null;
            List<int> cooldownValues = null;

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                cooldownKeys = new List<string>(cooldowns.Keys);
                cooldownValues = new List<int>(cooldowns.Values);
            }

            Scribe_Collections.Look(ref cooldownKeys, "cooldownKeys", LookMode.Value);
            Scribe_Collections.Look(ref cooldownValues, "cooldownValues", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                cooldowns = new Dictionary<string, int>();
                if (cooldownKeys != null && cooldownValues != null)
                {
                    for (int i = 0; i < cooldownKeys.Count; i++)
                    {
                        cooldowns[cooldownKeys[i]] = cooldownValues[i];
                    }
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            if (capacityBoosts.Count == 0 && statBoosts.Count == 0)
            {
                return null;
            }

            List<string> info = new List<string>();

            foreach (var kvp in capacityBoosts)
            {
                info.Add($"{kvp.Key}: +{(kvp.Value * 100):F1}%");
            }

            foreach (var kvp in statBoosts)
            {
                info.Add($"{kvp.Key}: +{kvp.Value:F2}");
            }

            return "Training Boosts:\n" + string.Join("\n", info);
        }
    }
}