using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace CG
{
    public class ParallScale
    {
        Vector3[] arrayPoints;
        float kx; 
        float ky; 
        float kz;
        float xCen;
        float yCen;
        int to;
        int from;

        public ParallScale(ref Vector3[] arrayPoints, float kx, float ky, float kz, float xCen, float yCen, int to, int from)
        {
            this.arrayPoints = arrayPoints;
            
            this.kx = kx;
            this.ky = ky;
            this.kz = kz;

            this.xCen = xCen;
            this.yCen = yCen;
            this.to = to;
            this.from = from;
        }

        public static void ScalePoint(Object obj)
        {
            ParallScale p = (ParallScale)obj;

            for (int i = p.to; i <= p.from; i++)
            {
                p.arrayPoints[i].X = p.xCen + p.kx * (p.arrayPoints[i].X - p.xCen);
                p.arrayPoints[i].Y = p.yCen + p.ky * (p.arrayPoints[i].Y - p.yCen);
                p.arrayPoints[i].Z = 100 + p.kz * (p.arrayPoints[i].Z - 100);
            }
        }
    }
}
