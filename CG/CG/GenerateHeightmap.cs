using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace CG
{
    static public class GenerateHeightmap
    {
        static private void Check(ref int val, int size)
        {
            if (val < 0)
            {
                val = 0;
            }
            else if (val > size)
            {
                val = size;
            }
        }

        static private void InitMem(double[][] Heightmap, int size)
        {
            for (int i = 0; i < Heightmap.Length; i++)
            {
                Heightmap[i] = new double[size];
            }
        }

        static private void InitMem(int[][] Faces, int size)
        {
            for (int i = 0; i < Faces.Length; i++)
            {
                Faces[i] = new int[size];
            }
        }

        static public double[][] Hills(int SizeX, int SizeY, bool Smoothing, bool Valley, int countHills)
        {
            double[][] Heightmap = new double[SizeX][];
            InitMem(Heightmap, SizeY);


            Random rand = new Random();

            double Radius;

            double x, y;

            double t;

            int min = 0;
            double max = double.MinValue;

            for (int k = 0; k < countHills; k++)

            {

                Radius = rand.NextDouble() * 10;

                x = SizeX * rand.NextDouble();

                y = SizeY * rand.NextDouble();

                int fromX = (int)(x - Radius);
                Check(ref fromX, SizeX);

                int fromY = (int)(y - Radius);
                Check(ref fromY, SizeY);

                int toX = (int)(x + Radius) + 1;
                Check(ref toX, SizeX);

                int toY = (int)(y + Radius) + 1;
                Check(ref toY, SizeY);



                for (int i = fromX; i < toX; i++)

                    for (int j = fromY; j < toY; j++)

                    {

                        t = Radius * Radius - ((i - x) * (i - x) + (j - y) * (j - y));

                        if (t > 0) Heightmap[i][j] += t;

                        if (max < Heightmap[i][j]) max = Heightmap[i][j];

                    }

            }

            double coef = 1 / ((max - min));

            int flagS = 0;
            if (Smoothing)
            {
                flagS = 1;
            }
            for (int i = flagS; i < SizeX - flagS; i++)
            {

                for (int j = flagS; j < SizeY - flagS; j++)

                {

                    if (Smoothing)

                        Heightmap[i][j] = (Heightmap[i - 1][j - 1] + Heightmap[i - 1][j] + Heightmap[i - 1][j + 1] +

                        Heightmap[i][j - 1] + Heightmap[i][j] + Heightmap[i][j + 1] +

                        Heightmap[i + 1][j - 1] + Heightmap[i + 1][j] + Heightmap[i + 1][j + 1] - 9.0 * min) / (9.0 * (max - min));

                    else
                    {
                        Heightmap[i][j] = (Heightmap[i][j] - min) * coef;
                    }

                    if (Valley)

                        Heightmap[i][j] = Math.Sqrt(Heightmap[i][j]);
                }
            }

            if (Smoothing)
            {
                for (int i = 0; i < SizeX; i++)
                {
                    Heightmap[0][i] = (Heightmap[0][i] - min) * coef;
                }

                for (int i = 0; i < SizeX; i++)
                {
                    Heightmap[SizeY - 1][i] = (Heightmap[SizeY - 1][i] - min) * coef;
                }

                for (int i = 0; i < SizeX; i++)
                {
                    Heightmap[i][0] = (Heightmap[i][0] - min) * coef;
                }

                for (int i = 0; i < SizeX; i++)
                {
                    Heightmap[i][SizeY - 1] = (Heightmap[i][SizeY - 1] - min) * coef;
                }
            }

            return Heightmap;
        }

        static public int[][] Faces(int SizeX, int SizeY, int step = 1)
        {
            int size = 1 + (SizeX - (step + 1)) / step;

            int[][] faces = new int[size * size * 2][];
            InitMem(faces, 3);

            int count = 0;
            for (int i = 0; i < SizeX - 1; i+= step)
            {
                for (int j = 0; j < SizeY - 1; j += step)
                {
                    int actV1 = (i * SizeX + j);
                    int actV2 = actV1 + 1;

                    faces[count][0] = actV1;
                    faces[count][1] = actV1 + step;
                    faces[count][2] = actV1 + SizeX * step;

                    count++;

                    faces[count][0] = actV2;
                    faces[count][1] = actV2 + (SizeX - 1) * step;
                    faces[count][2] = actV2 + SizeX * step;

                    count++;
                }
            }

            return faces;
        }
    }
}
