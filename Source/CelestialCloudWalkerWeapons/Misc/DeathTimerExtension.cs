using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using System.Reflection;

public class DeathTimerExtension : DefModExtension
{
    public string hediffToWatch;

    public List<string> hediffsToWatch = new List<string>();

    public List<string> AllHediffsToWatch
    {
        get
        {
            var all = new List<string>(hediffsToWatch);
            if (!hediffToWatch.NullOrEmpty() && !all.Contains(hediffToWatch))
                all.Add(hediffToWatch);
            return all;
        }
    }

    public string activationMessage = "{0} has activated a deadly power. Their lifespan has been drastically shortened.";
    public string deathMessage = "{0} has succumbed to the curse, their body unable to sustain the supernatural power any longer.";

    public string curseHediffLabel = "cursed";
    public string curseHediffDescription = "This person is cursed to die before their 25th birthday due to using forbidden power.";
    public string curseStageLabel = "cursed";

    public DeathCauseType deathCause = DeathCauseType.HeartFailure;

    public int maxAgeInYears = 25;
    public int minDaysEarlyDeath = 1;
    public int maxDaysEarlyDeath = 365;
    public int immediateDeathMinDays = 1;
    public int immediateDeathMaxDays = 30;
    public bool createCurseHediff = true;
    public bool activateOnGeneAdd = false;
}

public enum DeathCauseType
{
    HeartFailure,
    OrganFailure,
    BloodLoss,
    Asphyxiation,
    Random
}

public static class DeathTimerManager
{
    private static Dictionary<Gene, DeathTimerData> activeDeathTimers = new Dictionary<Gene, DeathTimerData>();

    public class DeathTimerData : IExposable
    {
        public bool powerActivated = false;
        public int deathTick = -1;
        public Gene parentGene;

        public DeathTimerData() { }
        public DeathTimerData(Gene gene) { parentGene = gene; }

        public bool HasDeathTimer => deathTick > 0;
        public Pawn Pawn => parentGene?.pawn;

        public int DaysUntilDeath
        {
            get
            {
                if (deathTick <= 0) return -1;
                int ticksLeft = deathTick - Find.TickManager.TicksGame;
                return ticksLeft <= 0 ? 0 : ticksLeft / 60000;
            }
        }

        public DeathTimerExtension Extension => parentGene?.def?.GetModExtension<DeathTimerExtension>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref powerActivated, "powerActivated", false);
            Scribe_Values.Look(ref deathTick, "deathTick", -1);
            Scribe_References.Look(ref parentGene, "parentGene");
        }
    }

    public static bool HasDeathTimer(Gene gene) => activeDeathTimers.ContainsKey(gene);

    public static DeathTimerData GetDeathTimer(Gene gene)
    {
        activeDeathTimers.TryGetValue(gene, out DeathTimerData data);
        return data;
    }

    public static bool PowerActivated(Gene gene) => GetDeathTimer(gene)?.powerActivated == true;

    public static int DaysUntilDeath(Gene gene) => GetDeathTimer(gene)?.DaysUntilDeath ?? -1;

    internal static void RegisterGene(Gene gene)
    {
        if (gene?.def?.HasModExtension<DeathTimerExtension>() != true) return;
        if (activeDeathTimers.ContainsKey(gene)) return;

        var deathTimer = new DeathTimerData(gene);
        activeDeathTimers[gene] = deathTimer;

        if (gene.def.GetModExtension<DeathTimerExtension>().activateOnGeneAdd)
            ActivatePower(gene);
    }

    internal static void UnregisterGene(Gene gene) => activeDeathTimers.Remove(gene);

    internal static void TickGene(Gene gene)
    {
        if (!activeDeathTimers.TryGetValue(gene, out DeathTimerData deathTimer)) return;
        var extension = deathTimer.Extension;
        if (extension == null || deathTimer.Pawn == null) return;

        if (Find.TickManager.TicksGame % 60 != 0) return;

        if (!extension.activateOnGeneAdd)
            CheckForPowerActivation(deathTimer);

        CheckDeathTimer(deathTimer);
    }

    private static void CheckForPowerActivation(DeathTimerData deathTimer)
    {
        if (deathTimer.powerActivated) return;
        if (deathTimer.Extension == null || deathTimer.Pawn?.health?.hediffSet == null) return;

        var extension = deathTimer.Extension;
        var hediffsToCheck = extension.AllHediffsToWatch;

        if (hediffsToCheck.NullOrEmpty())
        {
            Log.WarningOnce(
                $"[DeathTimer] Gene '{deathTimer.parentGene?.def?.defName}' has DeathTimerExtension " +
                "but no hediffToWatch or hediffsToWatch set. Power will never auto-activate.",
                deathTimer.parentGene?.def?.shortHash ?? 0);
            return;
        }

        foreach (string hediffName in hediffsToCheck)
        {
            if (hediffName.NullOrEmpty()) continue;

            HediffDef targetHediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediffName);
            if (targetHediffDef == null)
            {
                Log.WarningOnce(
                    $"[DeathTimer] Could not find hediff '{hediffName}' " +
                    $"for gene '{deathTimer.parentGene?.def?.defName}'.",
                    hediffName.GetHashCode());
                continue;
            }

            Hediff targetHediff = deathTimer.Pawn.health.hediffSet.GetFirstHediffOfDef(targetHediffDef);
            if (targetHediff != null && targetHediff.Severity > 0)
            {
                ActivatePower(deathTimer.parentGene);
                return;
            }
        }
    }

    public static void ActivatePower(Gene gene)
    {
        if (!activeDeathTimers.TryGetValue(gene, out DeathTimerData deathTimer)) return;
        if (deathTimer.powerActivated || deathTimer.Extension == null || deathTimer.Pawn == null) return;

        var extension = deathTimer.Extension;
        deathTimer.powerActivated = true;

        long currentAge = deathTimer.Pawn.ageTracker.AgeBiologicalTicks;
        long maxAgeInTicks = (long)extension.maxAgeInYears * 3600000L;

        if (currentAge >= maxAgeInTicks)
        {
            int days = Rand.Range(extension.immediateDeathMinDays, extension.immediateDeathMaxDays + 1);
            deathTimer.deathTick = Find.TickManager.TicksGame + days * 60000;
        }
        else
        {
            long ticksUntilMaxAge = maxAgeInTicks - currentAge;
            long earlyDeathTicks = (long)Rand.Range(extension.minDaysEarlyDeath, extension.maxDaysEarlyDeath + 1) * 60000L;
            deathTimer.deathTick = Find.TickManager.TicksGame + (int)(ticksUntilMaxAge - earlyDeathTicks);

            if (deathTimer.deathTick < Find.TickManager.TicksGame + 60000)
                deathTimer.deathTick = Find.TickManager.TicksGame + 60000;
        }

        if (!extension.activationMessage.NullOrEmpty())
            Messages.Message(string.Format(extension.activationMessage, deathTimer.Pawn.Name.ToStringShort),
                deathTimer.Pawn, MessageTypeDefOf.NegativeEvent);

        if (extension.createCurseHediff)
            CreateCurseHediff(deathTimer);
    }

    private static void CreateCurseHediff(DeathTimerData deathTimer)
    {
        var extension = deathTimer.Extension;
        string defName = $"DeathCurse_{deathTimer.parentGene.def.defName}";
        HediffDef curse = DefDatabase<HediffDef>.GetNamedSilentFail(defName);

        if (curse == null)
        {
            curse = new HediffDef
            {
                defName = defName,
                label = extension.curseHediffLabel,
                description = extension.curseHediffDescription,
                hediffClass = typeof(Hediff_DeathCurse),
                defaultLabelColor = new UnityEngine.Color(0.8f, 0.2f, 0.2f),
                makesSickThought = false,
                maxSeverity = 1.0f,
                initialSeverity = 1.0f,
                everCurableByItem = false,
                tendable = false,
                isBad = true,
                stages = new List<HediffStage>
                {
                    new HediffStage { label = extension.curseStageLabel, minSeverity = 0f }
                }
            };
            DefDatabase<HediffDef>.Add(curse);
        }

        Hediff hediff = HediffMaker.MakeHediff(curse, deathTimer.Pawn);
        hediff.Severity = 1f;
        deathTimer.Pawn.health.AddHediff(hediff);
    }

    private static void CheckDeathTimer(DeathTimerData deathTimer)
    {
        if (deathTimer.deathTick <= 0 || Find.TickManager.TicksGame < deathTimer.deathTick) return;
        KillPawn(deathTimer);
    }

    private static void KillPawn(DeathTimerData deathTimer)
    {
        if (deathTimer.Pawn?.Dead == true || deathTimer.Extension == null) return;

        var extension = deathTimer.Extension;
        if (!extension.deathMessage.NullOrEmpty())
            Messages.Message(string.Format(extension.deathMessage, deathTimer.Pawn.Name.ToStringShort),
                deathTimer.Pawn, MessageTypeDefOf.PawnDeath);

        DeathCauseType cause = extension.deathCause;
        if (cause == DeathCauseType.Random)
        {
            var valid = System.Enum.GetValues(typeof(DeathCauseType))
                .Cast<DeathCauseType>().Where(v => v != DeathCauseType.Random).ToArray();
            cause = valid[Rand.Range(0, valid.Length)];
        }

        switch (cause)
        {
            case DeathCauseType.HeartFailure: KillByHeartFailure(deathTimer.Pawn); break;
            case DeathCauseType.OrganFailure: KillByOrganFailure(deathTimer.Pawn); break;
            case DeathCauseType.BloodLoss: KillByBloodLoss(deathTimer.Pawn); break;
            case DeathCauseType.Asphyxiation: KillByAsphyxiation(deathTimer.Pawn); break;
            default: KillByHeartFailure(deathTimer.Pawn); break;
        }
    }

    private static void KillByHeartFailure(Pawn pawn)
    {
        var heart = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(x => x.def.defName == "Heart");
        var dmg = heart != null
            ? new DamageInfo(DamageDefOf.Deterioration, 999f, 999f, -1f, null, heart)
            : new DamageInfo(DamageDefOf.Deterioration, 999f);
        dmg.SetIgnoreArmor(true);
        pawn.TakeDamage(dmg);
    }

    private static void KillByOrganFailure(Pawn pawn)
    {
        var parts = pawn.health.hediffSet.GetNotMissingParts();
        foreach (string name in new[] { "Heart", "Liver", "Kidney", "Lung" })
        {
            var organ = parts.FirstOrDefault(x => x.def.defName == name);
            if (organ != null)
            {
                var dmg = new DamageInfo(DamageDefOf.Deterioration, 999f, 999f, -1f, null, organ);
                dmg.SetIgnoreArmor(true);
                pawn.TakeDamage(dmg);
                return;
            }
        }
        var fallback = new DamageInfo(DamageDefOf.Deterioration, 999f);
        fallback.SetIgnoreArmor(true);
        pawn.TakeDamage(fallback);
    }

    private static void KillByBloodLoss(Pawn pawn)
    {
        Hediff bloodLoss = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, pawn);
        bloodLoss.Severity = 1.0f;
        pawn.health.AddHediff(bloodLoss);
        var dmg = new DamageInfo(DamageDefOf.Cut, 50f);
        dmg.SetIgnoreArmor(true);
        pawn.TakeDamage(dmg);
    }

    private static void KillByAsphyxiation(Pawn pawn)
    {
        var lungs = pawn.health.hediffSet.GetNotMissingParts()
            .Where(x => x.def.defName == "Lung").ToList();
        if (lungs.Any())
        {
            foreach (var lung in lungs)
            {
                var dmg = new DamageInfo(DamageDefOf.Deterioration, 999f, 999f, -1f, null, lung);
                dmg.SetIgnoreArmor(true);
                pawn.TakeDamage(dmg);
            }
        }
        else
        {
            var dmg = new DamageInfo(DamageDefOf.Deterioration, 999f);
            dmg.SetIgnoreArmor(true);
            pawn.TakeDamage(dmg);
        }
    }

    public static void ExposeData()
    {
        var geneKeys = activeDeathTimers.Keys.ToList();
        var timerValues = activeDeathTimers.Values.ToList();

        Scribe_Collections.Look(ref geneKeys, "deathTimerGenes", LookMode.Reference);
        Scribe_Collections.Look(ref timerValues, "deathTimerData", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            activeDeathTimers.Clear();
            if (geneKeys != null && timerValues != null)
            {
                for (int i = 0; i < geneKeys.Count && i < timerValues.Count; i++)
                {
                    if (geneKeys[i] != null && timerValues[i] != null)
                    {
                        timerValues[i].parentGene = geneKeys[i];
                        activeDeathTimers[geneKeys[i]] = timerValues[i];
                    }
                }
            }
        }
    }
}

[HarmonyPatch]
public static class DeathTimerPatches
{
    [HarmonyPatch(typeof(Gene), "PostAdd")]
    [HarmonyPostfix]
    public static void Gene_PostAdd_Postfix(Gene __instance) => DeathTimerManager.RegisterGene(__instance);

    [HarmonyPatch(typeof(Gene), "PostRemove")]
    [HarmonyPostfix]
    public static void Gene_PostRemove_Postfix(Gene __instance) => DeathTimerManager.UnregisterGene(__instance);

    [HarmonyPatch(typeof(Gene), "Tick")]
    [HarmonyPostfix]
    public static void Gene_Tick_Postfix(Gene __instance) => DeathTimerManager.TickGene(__instance);

    [HarmonyPatch(typeof(Game), "ExposeData")]
    [HarmonyPostfix]
    public static void Game_ExposeData_Postfix() => DeathTimerManager.ExposeData();
}

public static class DeathTimerExtensions
{
    public static bool HasDeathTimer(this Gene gene) => DeathTimerManager.HasDeathTimer(gene);
    public static bool PowerActivated(this Gene gene) => DeathTimerManager.PowerActivated(gene);
    public static int DaysUntilDeath(this Gene gene) => DeathTimerManager.DaysUntilDeath(gene);
    public static DeathTimerManager.DeathTimerData GetDeathTimer(this Gene gene) => DeathTimerManager.GetDeathTimer(gene);
    public static void ActivateDeathTimer(this Gene gene) => DeathTimerManager.ActivatePower(gene);

    public static List<Gene> GetGenesWithActiveDeathTimers(this Pawn pawn)
    {
        if (pawn.genes?.GenesListForReading == null) return new List<Gene>();
        return pawn.genes.GenesListForReading.Where(g => g.HasDeathTimer() && g.PowerActivated()).ToList();
    }

    public static DeathTimerManager.DeathTimerData GetMostUrgentDeathTimer(this Pawn pawn)
    {
        return pawn.GetGenesWithActiveDeathTimers()
            .Select(g => g.GetDeathTimer())
            .Where(dt => dt?.HasDeathTimer == true)
            .OrderBy(dt => dt.DaysUntilDeath)
            .FirstOrDefault();
    }
}

public class Hediff_DeathCurse : HediffWithComps
{
    public override void PostMake() { base.PostMake(); Severity = 1f; }
    public override bool ShouldRemove => false;

    public override string TipStringExtra
    {
        get
        {
            string baseString = base.TipStringExtra;
            var mostUrgent = pawn.GetMostUrgentDeathTimer();
            if (mostUrgent?.HasDeathTimer == true)
            {
                int days = mostUrgent.DaysUntilDeath;
                string time = days <= 0 ? "Death imminent" : days == 1 ? "1 day remaining" : $"{days} days remaining";
                baseString += $"\n\nTime remaining: {time}";
            }
            return baseString;
        }
    }
}