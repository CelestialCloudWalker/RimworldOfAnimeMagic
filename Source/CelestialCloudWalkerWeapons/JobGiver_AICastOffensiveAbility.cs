﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse.AI;
using Verse;

namespace AnimeArsenal
{
    // Token: 0x02000133 RID: 307
    public class JobGiver_AICastOffensiveAbility : JobGiver_AICastAbility
    {
        // Token: 0x06000529 RID: 1321 RVA: 0x00018FB4 File Offset: 0x000171B4
        protected override LocalTargetInfo GetTarget(Pawn caster, Ability ability)
        {
            float maxRange = ability.def.verbProperties.range;
            return GenClosest.ClosestThingReachable(caster.Position, caster.Map, ThingRequest.ForGroup(ThingRequestGroup.Pawn), PathEndMode.Touch, TraverseParms.For(caster, Danger.Deadly, TraverseMode.ByPawn, false, false, false), maxRange, delegate (Thing t)
            {
                Pawn p = t as Pawn;
                return p != null && p.HostileTo(caster) && !p.Downed && ability.Activate(new LocalTargetInfo(caster), new LocalTargetInfo(p));
            }, null, 0, -1, false, RegionType.Set_Passable, false);
        }

        // Token: 0x0600052A RID: 1322 RVA: 0x0001903C File Offset: 0x0001723C
        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_AICastOffensiveAbility copy = (JobGiver_AICastOffensiveAbility)base.DeepCopy(resolve);
            copy.ability = this.ability;
            return copy;
        }

        // Token: 0x04000269 RID: 617
        public AbilityDef abilityDef;
    }
}