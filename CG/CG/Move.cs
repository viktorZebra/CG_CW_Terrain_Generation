using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.Threading;

namespace CG
{
    static public class Move
    {
        static int countThread = Environment.ProcessorCount;
        static Thread[] threadsArray = new Thread[countThread];

        public static void ThreadMove(Vector3[] arrayPoints, float dx, float dy, float dz)
        {
            int step = arrayPoints.Length / countThread;
            int to = 0;
            int from = step - 1;
            for (int i = 0; i < countThread; i++)
            {
                ParallMove p = new ParallMove(ref arrayPoints, dx, dy, dz, to, from);

                threadsArray[i] = new Thread(new ParameterizedThreadStart(ParallMove.MovePoints));
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

        public static void MovePoints(Vector3[] arrayPoints, double dx, double dy, double dz)
        {
            for (int i = 0; i < arrayPoints.Length; i++)
            {
                arrayPoints[i].X += (float)dx;
                arrayPoints[i].Y += (float)dy;
                arrayPoints[i].Z += (float)dz;
            }
        }
    }
}
