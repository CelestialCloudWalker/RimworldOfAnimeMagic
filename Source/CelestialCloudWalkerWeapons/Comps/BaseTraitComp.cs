using UnityEngine;

namespace AnimeArsenal
{
    public abstract class BaseTraitComp : ThingCompExt
    {
        public abstract string TraitName { get; }
        public abstract string Description { get; }

        public virtual Color TextColor => Color.cyan;
        public virtual string GetTraitName()
        {
            return $"<color={ColorUtility.ToHtmlStringRGB(TextColor)}>[{TraitName}]</color>";
        }

        public virtual string GetTraitDescription()
        {
            return Description;
        }

        public override string GetDescriptionPart()
        {
            return base.GetDescriptionPart() + GetTraitName() + "\r\n" + GetTraitDescription();
        }
    }
}