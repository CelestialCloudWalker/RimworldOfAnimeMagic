using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class DrawOffsetExt : DefModExtension
    {
        public Vector3 offset;


        public Vector3 GetOffsetForRot(Rot4 rot4)
        {
            Vector3 finalOffset = Vector3.zero;


            if (rot4 == Rot4.West)
            {
                finalOffset = new Vector3(-offset.x, offset.y, offset.z);
            }
            else
            {
                finalOffset = offset;
            }

            return finalOffset;
        }
    }
}