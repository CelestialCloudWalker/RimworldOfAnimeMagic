using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AnimeArsenal
{
    public class ProjectileProperties_AntiDemonBomb : ProjectileProperties
    {
        public List<GeneDef> targetGenes = new List<GeneDef>();
        public float damageAmount = 25f;
        public float armorPenetration = 0.3f;
        public HediffDef applyHediff = null;
        public float hediffSeverity = 0.5f;
        public EffecterDef impactEffecter = null;
        public EffecterDef explosionEffecter = null;
        public SoundDef explosionSound = null;
        public bool onlyHostileBuildings = true;
    }

    public class Projectile_AntiDemonBomb : Projectile
    {
        private ProjectileProperties_AntiDemonBomb ADBProps =>
            (ProjectileProperties_AntiDemonBomb)def.projectile;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 pos = Position;
            base.Impact(hitThing, blockedByShield);
            if (map == null) return;

            AntiDemonBombUtility.DetonateAt(
                pos,
                map,
                launcher as Pawn,
                ADBProps.targetGenes,
                def.projectile.damageDef,
                ADBProps.damageAmount,
                def.projectile.explosionRadius,
                ADBProps.armorPenetration,
                ADBProps.applyHediff,
                ADBProps.hediffSeverity,
                ADBProps.impactEffecter,
                ADBProps.explosionEffecter,
                ADBProps.explosionSound,
                ADBProps.onlyHostileBuildings
            );
        }
    }
}