using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace CG
{
    class ParallMove
    {
        Vector3[] arrayPoints;
        float dx;
        float dy;
        float dz;
        int to;
        int from;

        public ParallMove(ref Vector3[] arrayPoints, float dx, float dy, float dz, int to, int from)
        {
            this.arrayPoints = arrayPoints;

            this.dx = dx;
            this.dy = dy;
            this.dz = dz;

            this.to = to;
            this.from = from;
        }

        public static void MovePoints(Object obj)
        {
            ParallMove p = (ParallMove)obj;

            for (int i = p.to; i <= p.from; i++)
            {
                p.arrayPoints[i].X += p.dx;
                p.arrayPoints[i].Y += p.dy;
                p.arrayPoints[i].Z += p.dz;
            }
        }
    }
}
