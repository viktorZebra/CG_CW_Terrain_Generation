using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Threading;

namespace CG
{
    public static class Scale
    {
        static int countThread = Environment.ProcessorCount;
        static Thread[] threadsArray = new Thread[countThread];

        static void ScalePoint(ref Vector3 point, float kx, float ky, float kz, float xCen, float yCen)
        {
            point.X = xCen + kx * (point.X - xCen);
            point.Y = yCen + ky * (point.Y - yCen);
            point.Z = 100 + kz * (point.Z - 100);
        }

        public static void ThreadScale(Vector3[] arrayPoints, float kx, float ky, float kz, float xCen, float yCen)
        {
            int step = arrayPoints.Length / countThread;
            int to = 0;
            int from = step - 1;
            for (int i = 0; i < countThread; i++)
            {
                ParallScale p = new ParallScale(ref arrayPoints, kx, ky, kz, xCen, yCen, to, from);

                threadsArray[i] = new Thread(new ParameterizedThreadStart(ParallScale.ScalePoint));
                threadsArray[i].Start(p);

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

        public static void ScalePoints(Vector3[] arrayPoints, float kx, float ky, float kz, float xCen, float yCen)
        {
            for (int i = 0; i < arrayPoints.Length; i++)
                ScalePoint(ref arrayPoints[i], kx, ky, kz, xCen, yCen);
        }
    }
}
