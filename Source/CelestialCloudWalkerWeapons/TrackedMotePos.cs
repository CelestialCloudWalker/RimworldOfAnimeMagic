using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace AnimeArsenal
{
    public class TrackedMotePos
    {
        public MoteDualAttached Mote;
        public int RemainingTicks;
        public IntVec3 SourcePos;
        public IntVec3 TargetPos;

        public TrackedMotePos(MoteDualAttached mote, IntVec3 sourcePos, IntVec3 targetPos, int lifetimeTicks)
        {
            Mote = mote;
            SourcePos = sourcePos;
            TargetPos = targetPos;
            RemainingTicks = lifetimeTicks;
        }
    }

}