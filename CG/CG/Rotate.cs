using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Threading;

namespace CG
{
    public static class Rotate
    {
        static int countThread = Environment.ProcessorCount;
        static Thread[] threadsArray = new Thread[countThread];

        private static double DegToRadCos(double angle)
        {
            return Math.Cos(angle * Math.PI / 180);
        }

        private static double DegToRadSin(double angle)
        {
            return Math.Sin(angle * Math.PI / 180);
        }

        static void RotateOX(ref Vector3 point, float cosAngle, float sinAngle, float xCen, float yCen)
        {
            float y = yCen + (point.Y - yCen) * cosAngle - (point.Z - 100) * sinAngle;
            float z = 100 + (point.Y - yCen) * sinAngle + (point.Z - 100) * cosAngle;

            point.Y = y;
            point.Z = z;
        }

        static void RotateOY(ref Vector3 point, float cosAngle, float sinAngle, float xCen, float yCen)
        {
            float x = xCen + (point.X - xCen) * cosAngle + point.Z * sinAngle;
            float z = 100 - (point.X - xCen) * sinAngle + (point.Z - 100) * cosAngle;

            point.X = x;
            point.Z = z;
        }

        static void RotateOZ(ref Vector3 point, float cosAngle, float sinAngle, float xCen, float yCen)
        {
            float x = xCen + (point.X - xCen) * cosAngle - (point.Y - yCen) * sinAngle;
            float y = yCen + (point.X - xCen) * sinAngle + (point.Y - yCen) * cosAngle;

            point.X = x;
            point.Y = y;
        }

        public static void ThreadRotate(Vector3[] arrayPoints, double angle, int axis, float xCen, float yCen)
        {
            float cosAngle = (float)DegToRadCos(angle);
            float sinAngle = (float)DegToRadSin(angle);

            int step = arrayPoints.Length / countThread;
            int to = 0;
            int from = step - 1;
            for (int i = 0; i < countThread; i++)
            {
                ParallRotate p = new ParallRotate(ref arrayPoints, angle, xCen, yCen, to, from);

                if (axis == 1)
                {
                    threadsArray[i] = new Thread(new ParameterizedThreadStart(ParallRotate.RotateOX));
                    threadsArray[i].Start(p);
                }
                else if (axis == 2)
                {
                    threadsArray[i] = new Thread(new ParameterizedThreadStart(ParallRotate.RotateOY));
                    threadsArray[i].Start(p);
                }
                else if (axis == 3)
                {
                    threadsArray[i] = new Thread(new ParameterizedThreadStart(ParallRotate.RotateOZ));
                    threadsArray[i].Start(p);
                }

                if (i == countThread - 2)
                {
                    to = from + 1;
                    from = arrayPoints.Length - 1;
                }
                else
                {
                    to = from + 1;
                    from = to + step;
                }
            }

            foreach (Thread thread in threadsArray)
            {
                thread.Join();
            }
        }

        public static void RotatePoints(Vector3[] arrayPoints, double angle, int axis, float xCen, float yCen)
        {
            float cosAngle = (float)DegToRadCos(angle);
            float sinAngle = (float)DegToRadSin(angle);

            if (axis == 1)
            {
                for (int i = 0; i < arrayPoints.Length; i++)
                {
                    RotateOX(ref arrayPoints[i], cosAngle, sinAngle, xCen, yCen);
                }
            }
            else if (axis == 2)
            {
                for (int i = 0; i < arrayPoints.Length; i++)
                {
                    RotateOY(ref arrayPoints[i], cosAngle, sinAngle, xCen, yCen);
                }
            }
            else if (axis == 3)
            {
                for (int i = 0; i < arrayPoints.Length; i++)
                {
                    RotateOZ(ref arrayPoints[i], cosAngle, sinAngle, xCen, yCen);
                }
            }
        }
    }
}
