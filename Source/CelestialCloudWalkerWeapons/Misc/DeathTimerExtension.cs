using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using System.Reflection;


public class DeathTimerExtension : DefModExtension
{
    public string hediffToWatch; 
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

[StaticConstructorOnStartup]
public static class DeathTimerManager
{
    private static Dictionary<Gene, DeathTimerData> activeDeathTimers = new Dictionary<Gene, DeathTimerData>();

    static DeathTimerManager()
    {
        var harmony = new Harmony("DeathTimerExtension.Patches");
        harmony.PatchAll();
    }

    public class DeathTimerData : IExposable
    {
        public bool powerActivated = false;
        public int deathTick = -1;
        public Gene parentGene;

        public DeathTimerData() { }

        public DeathTimerData(Gene gene)
        {
            parentGene = gene;
        }

        public bool HasDeathTimer => deathTick > 0;
        public Pawn Pawn => parentGene?.pawn;

        public int DaysUntilDeath
        {
            get
            {
                if (deathTick <= 0) return -1;
                int ticksRemaining = deathTick - Find.TickManager.TicksGame;
                return ticksRemaining <= 0 ? 0 : ticksRemaining / 60000;
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

    public static bool HasDeathTimer(Gene gene)
    {
        return activeDeathTimers.ContainsKey(gene);
    }

    public static DeathTimerData GetDeathTimer(Gene gene)
    {
        activeDeathTimers.TryGetValue(gene, out DeathTimerData data);
        return data;
    }

    public static bool PowerActivated(Gene gene)
    {
        var timer = GetDeathTimer(gene);
        return timer?.powerActivated == true;
    }

    public static int DaysUntilDeath(Gene gene)
    {
        var timer = GetDeathTimer(gene);
        return timer?.DaysUntilDeath ?? -1;
    }

    internal static void RegisterGene(Gene gene)
    {
        if (gene?.def?.HasModExtension<DeathTimerExtension>() == true && !activeDeathTimers.ContainsKey(gene))
        {
            var deathTimer = new DeathTimerData(gene);
            activeDeathTimers[gene] = deathTimer;

            var extension = gene.def.GetModExtension<DeathTimerExtension>();
            if (extension.activateOnGeneAdd)
            {
                ActivatePower(gene);
            }
        }
    }

    internal static void UnregisterGene(Gene gene)
    {
        activeDeathTimers.Remove(gene);
    }

    internal static void TickGene(Gene gene)
    {
        if (!activeDeathTimers.TryGetValue(gene, out DeathTimerData deathTimer)) return;

        var extension = deathTimer.Extension;
        if (extension == null || deathTimer.Pawn == null) return;

        
        if (Find.TickManager.TicksGame % 60 == 0)
        {
            if (!extension.activateOnGeneAdd)
            {
                CheckForPowerActivation(deathTimer);
            }
            CheckDeathTimer(deathTimer);
        }
    }

    private static void CheckForPowerActivation(DeathTimerData deathTimer)
    {
        if (deathTimer.powerActivated || deathTimer.Extension == null || deathTimer.Pawn?.health?.hediffSet == null) return;

        var extension = deathTimer.Extension;
        HediffDef targetHediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(extension.hediffToWatch);
        if (targetHediffDef == null)
        {
            Log.Warning($"DeathTimerExtension: Could not find hediff '{extension.hediffToWatch}' for gene {deathTimer.parentGene.def.defName}");
            return;
        }

        Hediff targetHediff = deathTimer.Pawn.health.hediffSet.GetFirstHediffOfDef(targetHediffDef);
        if (targetHediff != null && targetHediff.Severity > 0)
        {
            ActivatePower(deathTimer.parentGene);
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
            int daysUntilDeath = Rand.Range(extension.immediateDeathMinDays, extension.immediateDeathMaxDays + 1);
            deathTimer.deathTick = Find.TickManager.TicksGame + (daysUntilDeath * 60000);
        }
        else
        {
            long ticksUntilMaxAge = maxAgeInTicks - currentAge;
            int earlyDeathDays = Rand.Range(extension.minDaysEarlyDeath, extension.maxDaysEarlyDeath + 1);
            long earlyDeathTicks = (long)earlyDeathDays * 60000L;
            deathTimer.deathTick = Find.TickManager.TicksGame + (int)(ticksUntilMaxAge - earlyDeathTicks);

            if (deathTimer.deathTick < Find.TickManager.TicksGame + 60000)
            {
                deathTimer.deathTick = Find.TickManager.TicksGame + 60000;
            }
        }

        if (!extension.activationMessage.NullOrEmpty())
        {
            string message = string.Format(extension.activationMessage, deathTimer.Pawn.Name.ToStringShort);
            Messages.Message(message, deathTimer.Pawn, MessageTypeDefOf.NegativeEvent);
        }

        if (extension.createCurseHediff)
        {
            CreateCurseHediff(deathTimer);
        }
    }

    private static void CreateCurseHediff(DeathTimerData deathTimer)
    {
        var extension = deathTimer.Extension;
        string curseDefName = $"DeathCurse_{deathTimer.parentGene.def.defName}";
        HediffDef curseDef = DefDatabase<HediffDef>.GetNamedSilentFail(curseDefName);

        if (curseDef == null)
        {
            curseDef = new HediffDef();
            curseDef.defName = curseDefName;
            curseDef.label = extension.curseHediffLabel; 
            curseDef.description = extension.curseHediffDescription; 
            curseDef.hediffClass = typeof(Hediff_DeathCurse);
            curseDef.defaultLabelColor = new UnityEngine.Color(0.8f, 0.2f, 0.2f);
            curseDef.makesSickThought = false;
            curseDef.maxSeverity = 1.0f;
            curseDef.initialSeverity = 1.0f;
            curseDef.everCurableByItem = false;
            curseDef.tendable = false;
            curseDef.isBad = true;

            curseDef.stages = new List<HediffStage>();
            HediffStage stage = new HediffStage();
            stage.label = extension.curseStageLabel; 
            stage.minSeverity = 0f;
            curseDef.stages.Add(stage);

            DefDatabase<HediffDef>.Add(curseDef);
        }

        Hediff curse = HediffMaker.MakeHediff(curseDef, deathTimer.Pawn);
        curse.Severity = 1f;
        deathTimer.Pawn.health.AddHediff(curse);
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
        {
            string message = string.Format(extension.deathMessage, deathTimer.Pawn.Name.ToStringShort);
            Messages.Message(message, deathTimer.Pawn, MessageTypeDefOf.PawnDeath);
        }

        // Choose death method based on configuration
        DeathCauseType causeToUse = extension.deathCause;
        if (causeToUse == DeathCauseType.Random)
        {
            var values = System.Enum.GetValues(typeof(DeathCauseType));
            var validValues = values.Cast<DeathCauseType>().Where(v => v != DeathCauseType.Random).ToArray();
            causeToUse = validValues[Rand.Range(0, validValues.Length)];
        }

        switch (causeToUse)
        {
            case DeathCauseType.HeartFailure:
                KillByHeartFailure(deathTimer.Pawn);
                break;
            case DeathCauseType.OrganFailure:
                KillByOrganFailure(deathTimer.Pawn);
                break;
            case DeathCauseType.BloodLoss:
                KillByBloodLoss(deathTimer.Pawn);
                break;
            case DeathCauseType.Asphyxiation:
                KillByAsphyxiation(deathTimer.Pawn);
                break;
            default:
                KillByHeartFailure(deathTimer.Pawn); 
                break;
        }
    }

    private static void KillByHeartFailure(Pawn pawn)
    {
        BodyPartRecord heart = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(x => x.def.defName == "Heart");
        if (heart != null)
        {
            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Deterioration, 999f, 999f, -1f, null, heart);
            damageInfo.SetIgnoreArmor(true);
            pawn.TakeDamage(damageInfo);
        }
        else
        {
            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Deterioration, 999f);
            damageInfo.SetIgnoreArmor(true);
            pawn.TakeDamage(damageInfo);
        }
    }

    private static void KillByOrganFailure(Pawn pawn)
    {
        var vitalOrgans = new[] { "Heart", "Liver", "Kidney", "Lung" };
        var bodyParts = pawn.health.hediffSet.GetNotMissingParts();

        foreach (string organName in vitalOrgans)
        {
            var organ = bodyParts.FirstOrDefault(x => x.def.defName == organName);
            if (organ != null)
            {
                DamageInfo damageInfo = new DamageInfo(DamageDefOf.Deterioration, 999f, 999f, -1f, null, organ);
                damageInfo.SetIgnoreArmor(true);
                pawn.TakeDamage(damageInfo);
                break; 
            }
        }

        if (vitalOrgans.All(organName => bodyParts.All(bp => bp.def.defName != organName)))
        {
            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Deterioration, 999f);
            damageInfo.SetIgnoreArmor(true);
            pawn.TakeDamage(damageInfo);
        }
    }

    private static void KillByBloodLoss(Pawn pawn)
    {
        Hediff bloodLoss = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, pawn);
        bloodLoss.Severity = 1.0f; 
        pawn.health.AddHediff(bloodLoss);

        DamageInfo damageInfo = new DamageInfo(DamageDefOf.Cut, 50f);
        damageInfo.SetIgnoreArmor(true);
        pawn.TakeDamage(damageInfo);
    }

    private static void KillByAsphyxiation(Pawn pawn)
    {
        var lungs = pawn.health.hediffSet.GetNotMissingParts().Where(x => x.def.defName == "Lung").ToList();
        if (lungs.Any())
        {
            foreach (var lung in lungs)
            {
                DamageInfo damageInfo = new DamageInfo(DamageDefOf.Deterioration, 999f, 999f, -1f, null, lung);
                damageInfo.SetIgnoreArmor(true);
                pawn.TakeDamage(damageInfo);
            }
        }
        else
        {
            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Deterioration, 999f);
            damageInfo.SetIgnoreArmor(true);
            pawn.TakeDamage(damageInfo);
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
    public static void Gene_PostAdd_Postfix(Gene __instance)
    {
        DeathTimerManager.RegisterGene(__instance);
    }

    [HarmonyPatch(typeof(Gene), "PostRemove")]
    [HarmonyPostfix]
    public static void Gene_PostRemove_Postfix(Gene __instance)
    {
        DeathTimerManager.UnregisterGene(__instance);
    }

    [HarmonyPatch(typeof(Gene), "Tick")]
    [HarmonyPostfix]
    public static void Gene_Tick_Postfix(Gene __instance)
    {
        DeathTimerManager.TickGene(__instance);
    }

    [HarmonyPatch(typeof(Game), "ExposeData")]
    [HarmonyPostfix]
    public static void Game_ExposeData_Postfix()
    {
        DeathTimerManager.ExposeData();
    }
}

public static class DeathTimerExtensions
{
    public static bool HasDeathTimer(this Gene gene)
    {
        return DeathTimerManager.HasDeathTimer(gene);
    }

    public static bool PowerActivated(this Gene gene)
    {
        return DeathTimerManager.PowerActivated(gene);
    }

    public static int DaysUntilDeath(this Gene gene)
    {
        return DeathTimerManager.DaysUntilDeath(gene);
    }

    public static DeathTimerManager.DeathTimerData GetDeathTimer(this Gene gene)
    {
        return DeathTimerManager.GetDeathTimer(gene);
    }

    public static void ActivateDeathTimer(this Gene gene)
    {
        DeathTimerManager.ActivatePower(gene);
    }

    public static List<Gene> GetGenesWithActiveDeathTimers(this Pawn pawn)
    {
        if (pawn.genes?.GenesListForReading == null) return new List<Gene>();

        return pawn.genes.GenesListForReading
            .Where(g => g.HasDeathTimer() && g.PowerActivated())
            .ToList();
    }

    public static DeathTimerManager.DeathTimerData GetMostUrgentDeathTimer(this Pawn pawn)
    {
        var activeTimers = pawn.GetGenesWithActiveDeathTimers()
            .Select(g => g.GetDeathTimer())
            .Where(dt => dt?.HasDeathTimer == true)
            .OrderBy(dt => dt.DaysUntilDeath);

        return activeTimers.FirstOrDefault();
    }
}

public class Hediff_DeathCurse : HediffWithComps
{
    public override void PostMake()
    {
        base.PostMake();
        this.Severity = 1f;
    }

    public override bool ShouldRemove => false;

    public override string TipStringExtra
    {
        get
        {
            string baseString = base.TipStringExtra;

            var deathTimers = pawn.GetGenesWithActiveDeathTimers();
            if (deathTimers.Any())
            {
                var mostUrgent = pawn.GetMostUrgentDeathTimer();
                if (mostUrgent != null && mostUrgent.HasDeathTimer)
                {
                    int days = mostUrgent.DaysUntilDeath;
                    string timeText = days <= 0 ? "Death imminent" :
                                    days == 1 ? "1 day remaining" :
                                    $"{days} days remaining";
                    baseString += $"\n\nTime remaining: {timeText}";
                }
            }

            return baseString;
        }
    }
}