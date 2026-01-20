using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public static class DamageScalingUtility
    {
        public static float GetScaledDamage(float baseDamage, Pawn caster, StatDef scaleStat = null, SkillDef scaleSkill = null, float skillMultiplier = 0.1f, bool debug = false)
        {
            if (caster == null) return baseDamage;

            float multiplier = 1f;

            if (scaleStat != null)
            {
                multiplier = caster.GetStatValue(scaleStat);
                if (debug)
                {
                    Log.Message($"[DamageScaling] {caster.LabelShort} base: {baseDamage}, stat {scaleStat.defName}: {multiplier}, final: {baseDamage * multiplier}");
                }
            }
            else if (scaleSkill != null)
            {
                int skillLevel = caster.skills?.GetSkill(scaleSkill)?.Level ?? 0;
                multiplier = 1f + (skillLevel * skillMultiplier);
                if (debug)
                {
                    Log.Message($"[DamageScaling] {caster.LabelShort} base: {baseDamage}, skill {scaleSkill.defName} Lv{skillLevel} x{skillMultiplier}: {multiplier}, final: {baseDamage * multiplier}");
                }
            }

            return baseDamage * multiplier;
        }
    }

    public class DamageScalingProperties
    {
        public StatDef scaleStat;
        public SkillDef scaleSkill;
        public float skillMultiplier = 0.1f;
        public bool debugScaling = false;

        public float GetScaledDamage(float baseDamage, Pawn caster)
        {
            return DamageScalingUtility.GetScaledDamage(baseDamage, caster, scaleStat, scaleSkill, skillMultiplier, debugScaling);
        }
    }
}