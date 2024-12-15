using Mono.Unix.Native;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{
    public class CompProperties_RangeToggle : CompProperties
    {
        public CompProperties_RangeToggle()
        {
            this.compClass = typeof(CompRangeToggle);
            Log.Message("[RangeToggle] CompProperties_RangeToggle constructor called");
        }
    }

    public class CompRangeToggle : ThingComp
    {
        private bool extendedRangeEnabled = false;

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);

            var equipmentComp = parent.GetComp<CompEquippable>();
            var extendedVerb = equipmentComp.AllVerbs
                .FirstOrDefault(v => v is Verb_ExtendedMeleeAttack) as Verb_ExtendedMeleeAttack;

            if (extendedVerb != null)
            {
                extendedVerb.SetRange(500f);
                Log.Message($"[RangeToggle] Range set to: {extendedVerb.EffectiveRange}");
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref extendedRangeEnabled, "extendedRangeEnabled", false);
        }


        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            Log.Message($"CompGetGizmosExtra");

            foreach (var item in base.CompGetWornGizmosExtra())
            {
                yield return item;
            }

            yield return ToggleAction();
        }


        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            Log.Message($"CompGetGizmosExtra");

            foreach (var item in base.CompGetGizmosExtra())
            {
                yield return item;
            }

            yield return ToggleAction();
        }

        private Command_Action ToggleAction()
        {
            return new Command_Action
            {
                defaultLabel = "Extended Range",
                defaultDesc = "Toggle extended range attacks",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", true),
                action = () =>
                {
                    extendedRangeEnabled = !extendedRangeEnabled;
                    var equipmentComp = parent.GetComp<CompEquippable>();
                    var extendedVerb = equipmentComp.AllVerbs
                        .FirstOrDefault(v => v is Verb_ExtendedMeleeAttack) as Verb_ExtendedMeleeAttack;

                    Log.Message($"[RangeToggle] Toggle pressed. EquipmentComp: {equipmentComp != null}, ExtendedVerb found: {extendedVerb != null}");

                    if (extendedVerb != null)
                    {
                        extendedVerb.SetRange(extendedRangeEnabled ? 500f : 1.5f);
                        Log.Message($"[RangeToggle] Range set to: {extendedVerb.EffectiveRange}");
                    }
                }
            };
        }
    }

}
