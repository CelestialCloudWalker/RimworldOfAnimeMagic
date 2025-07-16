using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class DrawOffsetExt : DefModExtension
    {
        public Vector3 offset;

        public Vector3 offsetNorth;
        public Vector3 offsetSouth;
        public Vector3 offsetEast;
        public Vector3 offsetWest;

        public Vector3 GetOffsetForRot(Rot4 rot4)
        {
            Vector3 finalOffset = Vector3.zero;

            if (rot4 == Rot4.North && offsetNorth != Vector3.zero)
            {
                finalOffset = offsetNorth;
            }
            else if (rot4 == Rot4.South && offsetSouth != Vector3.zero)
            {
                finalOffset = offsetSouth;
            }
            else if (rot4 == Rot4.East && offsetEast != Vector3.zero)
            {
                finalOffset = offsetEast;
            }
            else if (rot4 == Rot4.West && offsetWest != Vector3.zero)
            {
                finalOffset = offsetWest;
            }
            else
            {
                if (rot4 == Rot4.West)
                {
                    finalOffset = new Vector3(-offset.x, offset.y, -offset.z);
                }
                else if (rot4 == Rot4.North)
                {
                    finalOffset = new Vector3(offset.x, offset.y + 0.1f, offset.z);
                }
                else if (rot4 == Rot4.South)
                {
                    finalOffset = new Vector3(offset.x, offset.y - 0.1f, offset.z);
                }
                else
                {
                    finalOffset = offset;
                }
            }

            return finalOffset;
        }
    }
}