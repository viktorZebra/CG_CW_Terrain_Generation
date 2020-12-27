using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace CG
{
    public class ParallRotate
    {
        Vector3[] arrayPoints;
        double angle;
        float xCen;
        float yCen;
        int to;
        int from;

        public ParallRotate(ref Vector3[] arrayPoints, double angle, float xCen, float yCen, int to, int from)
        {
            this.arrayPoints = arrayPoints;
            this.angle = angle;
            this.xCen = xCen;
            this.yCen = yCen;
            this.to = to;
            this.from = from;
        }
        private static double DegToRadCos(double angle)
        {
            return Math.Cos(angle * Math.PI / 180);
        }

        private static double DegToRadSin(double angle)
        {
            return Math.Sin(angle * Math.PI / 180);
        }

        static public void RotateOX(Object obj)
        {
            ParallRotate p = (ParallRotate)obj;

            float cosAngle = (float)DegToRadCos(p.angle);
            float sinAngle = (float)DegToRadSin(p.angle);

            for (int i = p.to; i <= p.from; i++)
            {
                float y = p.yCen + (p.arrayPoints[i].Y - p.yCen) * cosAngle - (p.arrayPoints[i].Z - 100) * sinAngle;
                float z = 100 + (p.arrayPoints[i].Y - p.yCen) * sinAngle + (p.arrayPoints[i].Z - 100) * cosAngle;

                p.arrayPoints[i].Y = y;
                p.arrayPoints[i].Z = z;
            }
        }

        static public void RotateOY(Object obj)
        {
            ParallRotate p = (ParallRotate)obj;

            float cosAngle = (float)DegToRadCos(p.angle);
            float sinAngle = (float)DegToRadSin(p.angle);

            for (int i = p.to; i <= p.from; i++)
            {
                float x = p.xCen + (p.arrayPoints[i].X - p.xCen) * cosAngle + p.arrayPoints[i].Z * sinAngle;
                float z = 100 - (p.arrayPoints[i].X - p.xCen) * sinAngle + (p.arrayPoints[i].Z - 100) * cosAngle;


                p.arrayPoints[i].X = x;
                p.arrayPoints[i].Z = z;
            }
        }

        static public void RotateOZ(Object obj)
        {
            ParallRotate p = (ParallRotate)obj;

            float cosAngle = (float)DegToRadCos(p.angle);
            float sinAngle = (float)DegToRadSin(p.angle);

            for (int i = p.to; i <= p.from; i++)
            {
                float x = p.xCen + (p.arrayPoints[i].X - p.xCen) * cosAngle - (p.arrayPoints[i].Y - p.yCen) * sinAngle;
                float y = p.yCen + (p.arrayPoints[i].X - p.xCen) * sinAngle + (p.arrayPoints[i].Y - p.yCen) * cosAngle;

                p.arrayPoints[i].X = x;
                p.arrayPoints[i].Y = y;
            }
        }
    }
}
