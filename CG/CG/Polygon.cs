using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace CG
{
    static class Polygon
    {
        /// <summary>
        /// Поиск точек, лежащих внутри треугольника
        /// </summary>
        /// <param name="firstXPossible">Минимальная X координата</param>
        /// <param name="firstYPossible">Минимальная Y координата</param>
        /// <param name="lastXPossible">Максимальная X координата</param>
        /// <param name="lastYPossible">Максимальная Y координата</param>
        /// <param name="v">Вершины треугольника</param>
        static public void CalculatePointsInsideTriangle(ref List<Vector3> pointsInside, List<Vector3> v, int lastXPossible, int lastYPossible, int firstXPossible = 0, int firstYPossible = 0)
        {
            float yMax, yMin;
            float[] X = new float[3], Y = new float[3];

            for (int i = 0; i < 3; ++i)
            {
                X[i] = v[i].X;
                Y[i] = v[i].Y;
            }

            yMax = Y.Max();
            yMin = Y.Min();

            yMin = (float)Math.Round((yMin < firstYPossible) ? firstYPossible : yMin);
            yMax = (float)Math.Round((yMax < lastYPossible) ? yMax : lastYPossible);

            float x1 = 0, x2 = 0;
            double z1 = 0, z2 = 0;

            for (float yDot = yMin; yDot <= yMax; yDot++)
            {
                int fFirst = 1;
                for (int n = 0; n < 3; ++n)
                {
                    int n1 = (n == 2) ? 0 : n + 1;

                    if (yDot >= Math.Max(Y[n], Y[n1]) || yDot < Math.Min(Y[n], Y[n1])) // || Y[n] == Y[n1]  
                        continue; // точка вне

                    double m = (double)(Y[n] - yDot) / (Y[n] - Y[n1]);
                    if (fFirst == 0)
                    {
                        x2 = X[n] + (int)(m * (X[n1] - X[n]));
                        z2 = v[n].Z + m * (v[n1].Z - v[n].Z);
                    }
                    else
                    {
                        x1 = X[n] + (int)(m * (X[n1] - X[n]));
                        z1 = v[n].Z + m * (v[n1].Z - v[n].Z);
                    }
                    fFirst = 0;
                }

                if (x2 < x1)
                {
                    Swap(ref x1, ref x2);
                    Swap(ref z1, ref z2);
                }

                float xStart = (float)Math.Round((x1 < firstXPossible) ? firstXPossible : x1);
                float xEnd = (float)Math.Round((x2 < lastXPossible) ? x2 : lastXPossible);
                for (float xDot = xStart; xDot < xEnd; xDot++)
                {
                    double m = (double)(x1 - xDot) / (x1 - x2);
                    double zDot = z1 + m * (z2 - z1);

                    pointsInside.Add(new Vector3(xDot, yDot, (float)Math.Round(zDot)));
                }
            }
        }

        /// <summary>
        /// Получение вектора нормали к многоугольнику
        /// </summary>
        /// <returns></returns>
        static public Vector3 GetNormal(List<Vector3> v)
        {
            Vector3 v1 = v[1] - v[0];
            Vector3 v2 = v[0] - v[2];

            Vector3 N = Vector3.Cross(v1, v2);


            return Vector3.Normalize(N);
        }

        static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp;
            temp = lhs;
            lhs = rhs;
            rhs = temp;
        }
    }
}
