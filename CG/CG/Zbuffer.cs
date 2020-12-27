using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Numerics;

namespace CG
{
    public static class Zbuffer
    {
        /// <summary>
        /// Получение изображения сцены
        /// </summary>
        /// <param name="models">Список всех моделей сцены</param>
        /// <param name="size">Размер сцены</param>
        /// <returns></returns>
        static public float[][] GenBuffer(int width, int height, ref Bitmap landscape, int[][] faces, Vector3[] arrHeightmap, Vector3[] wordHeightmap, Vector3 lightDir, Color[] colorPix)
        {
            float[][] zBufSun = new float[height][];
            float[][] zBuf = new float[height][];
            InitBuf(ref zBuf, width, height);
            InitBuf(ref zBufSun, width, height);

            List<Vector3> viewPolygon = new List<Vector3>(3);
            List<Vector3> worldPolygon = new List<Vector3>(3);
            for (int i = 0; i < faces.Length; i++)
            {
                viewPolygon.Add(arrHeightmap[faces[i][0]]);
                viewPolygon.Add(arrHeightmap[faces[i][1]]);
                viewPolygon.Add(arrHeightmap[faces[i][2]]);

                worldPolygon.Add(wordHeightmap[faces[i][0]]);
                worldPolygon.Add(wordHeightmap[faces[i][1]]);
                worldPolygon.Add(wordHeightmap[faces[i][2]]);

                ProcessModel(zBufSun, zBuf, ref landscape, viewPolygon, worldPolygon, width, height, lightDir, colorPix[faces[i][0]]);

                viewPolygon.Clear();
                worldPolygon.Clear();
            }
            //addShadow(lightDir, zBuf, width, height, ref landscape, faces, wordHeightmap, colorPix);

            return zBuf;
        }


        static public void addShadow(Vector3 lightDir, List<Vector3> polygon, Vector3 N)
        {
            float ABC = N.X * polygon[0].X + N.Y * polygon[0].Y + N.Z * polygon[0].Z;
            float D = -ABC;
            //float t = -()

        }
        static public void addShadow(Vector3 lightDir, float[][] zBufSun, int width, int height, ref Bitmap landscape, int[][] faces, Vector3[] wordHeightmap, Color[] colorPix)
        {
            Vector3 v1 = new Vector3(0, 0, 10);

            double angleBetween = AngleBetween(v1, lightDir);

            List<Vector3> worldPolygon = new List<Vector3>(3);
            for (int i = 0; i < faces.Length; i++)
            {
                worldPolygon.Add(wordHeightmap[faces[i][0]]);
                worldPolygon.Add(wordHeightmap[faces[i][1]]);
                worldPolygon.Add(wordHeightmap[faces[i][2]]);

                List<Vector3> pointsInside = new List<Vector3>(100);
                Polygon.CalculatePointsInsideTriangle(ref pointsInside, worldPolygon, width, height);
                foreach (Vector3 point in pointsInside)
                {
                    int X = (int)Math.Round(point.X);
                    int Y = (int)Math.Round(point.Y);
                    int Z = (int)Math.Round(point.Z);

                    Vector3 sunPoint = Transformation.Transform(X, Y, Z, 0, 0, -90);

                    if (Y == 650)
                    {
                        continue;
                    }
                    if (sunPoint.Z < zBufSun[Y][X])
                    {
                        if (sunPoint.X > 0 && sunPoint.Y > 0 && sunPoint.X < 667 && sunPoint.Y < 650)
                        {
                            landscape.SetPixel((int)sunPoint.X, (int)sunPoint.Y, Color.FromArgb((int)(colorPix[faces[i][0]].R * 0.2), (int)(colorPix[faces[i][0]].G * 0.2), (int)(colorPix[faces[i][0]].B * 0.2)));
                        }
                    
                    }
                }

                worldPolygon.Clear();
            }

            //Vector3 v1 = new Vector3(0, 0, 10);
            //Vector3 v2 = new Vector3(1, -1, -1);

            //double angleBetween = AngleBetween(v1, v2);

            //for (int i = 0; i < height; i++)
            //    for (int j = 0; j < width; j++)
            //    {
            //        //PointTransform(ref point);
            //        int X = (int)Math.Round(point.X);
            //        int Y = (int)Math.Round(point.Y);
            //        int Z = (int)Math.Round(point.Z);

            //        if (!(X < 0 || X >= w || Y < 0 || Y >= h))
            //        {
            //            if (Z > buffer[Y][X])
            //            {
            //                buffer[Y][X] = Z;
            //                image.SetPixel(X, Y, clr);
            //            }

            //            Vector3 sunPoint = Transformation.Transform(X, Y, Z, angleBetween, angleBetween, angleBetween);

            //            if (Z > zBufSun[Y][X])
            //                zBufSun[Y][X] = Z;
            //        }
            //    }
        }

        /// <summary>
        /// Установка углов между взглядом и источником света
        /// </summary>
        /*private void InitTeta()
        {
            tettax = sun.tetax;
            tettay = sun.tetay;
            tettaz = sun.tetaz;
        }*/

        /// <summary>
        /// Инициализация буффера, заполнение начальным значением
        /// </summary>
        /// <param name="buf">Буфер</param>
        /// <param name="w">Ширина буфера</param>
        /// <param name="h">Высота буфера</param>
        /// <param name="value">Начальное значение</param>
        private static void InitBuf(ref float[][] buf, int w, int h)
        {
            for (int i = 0; i < h; i++)
            {
                buf[i] = new float[w];
                for (int j = 0; j < w; j++)
                    buf[i][j] = float.MinValue;
            }
        }

        private static void InitBufSun(ref float[][] buf, int w, int h)
        {
            for (int i = 0; i < h; i++)
            {
                buf[i] = new float[w];
                for (int j = 0; j < w; j++)
                    buf[i][j] = float.MaxValue;
            }
        }

        /// <summary>
        /// Алгоритм нахождения теней (совмещение карты от лица наблюдателя и источника света)
        /// </summary>
        /// <returns></returns>
        static public void AddShadows(float[][] zBuf, int width, int height, ref Bitmap landscape)
        {
            float[][] ZbufFromSun = new float[height][];
            InitBuf(ref ZbufFromSun, width, height);

            for (int j = 0; j < width; j++)
            {
                for (int i = 0; i < height; i++)
                {
                    float z = zBuf[i][j];
                    if (z != float.MinValue)
                    {
                        Vector3 newCoord = Transformation.Transform(i, j, (int)z, 90, 180, 0);

                        Color curPixColor = landscape.GetPixel(j, i);
                        if (newCoord.X < 0 || newCoord.Y < 0 || newCoord.X >= width || newCoord.Y >= height)
                        {
                            landscape.SetPixel(j, i, curPixColor); //тени не считаются, чтобы увидеть эти места -> убрать эту строку;
                            continue;
                        }

                        if (ZbufFromSun[(int)newCoord.Y][(int)newCoord.X] > newCoord.Z + 5) // текущая точка невидима из источника света
                        {
                            landscape.SetPixel(j, i, Mix(Color.Black, curPixColor, 0.4f));
                        }
                        else
                        {
                            landscape.SetPixel(j, i, curPixColor);
                        }
                    }
                }
            }
        }

/*
        /// <summary>
        /// Объеденяет двумерный массив цветов в Bitmap
        /// </summary>
        /// <param name="splited">Двумерный массив цветов</param>
        /// <returns></returns>
        private Bitmap ConnectBitmap(Color[][] splited)
        {
            Bitmap b = new Bitmap(splited[0].Length, splited.Length);
            for (int i = 0; i < b.Width; i++)
            {
                for (int j = 0; j < b.Height; j++)
                {
                    b.SetPixel(i, j, splited[i][j]);
                }
            }
            return b;
        }

        #region Получить данные извне
        public Bitmap GetImage()
        {
            return img;
        }

        public Bitmap GetSunImage()
        {
            return imgFromSun;
        }

        public int[][] GetZbuf()
        {
            return Zbuf;
        }

        public int GetZ(int x, int y)
        {
            return Zbuf[y][x];
        }

        public int GetZ(Point p)
        {
            return Zbuf[p.Y][p.X];
        }*/
        

        /// <summary>
        /// Обрабока одной модели для занесения ее в буфер
        /// </summary>
        /// <param name="m">Модель</param>
        private static void ProcessModel(float[][] zBufSun, float[][] buffer, ref Bitmap landscape, List<Vector3> viewPolygon, List<Vector3> worldPolygon, int width, int height, Vector3 lightDir, Color colorPix)
        {
            Color objC = colorPix;

            Vector3 lightPos = Vector3.Normalize(lightDir);

            Vector3 N = Polygon.GetNormal(worldPolygon);
            if (N.Z < 0)
                N *= -1;

            float lightI = ((0.6f + 0.4f * Vector3.Dot(lightPos, N)));

            float buf = Vector3.Dot(lightPos, N);

            List<Vector3> pointsInside = new List<Vector3>(100);

            Polygon.CalculatePointsInsideTriangle(ref pointsInside, worldPolygon, width, height);
            foreach (Vector3 point in pointsInside)
            {

                Color clr = Color.FromArgb((int)(objC.R * lightI), (int)(objC.G * lightI), (int)(objC.B * lightI));
                ProcessPoint(lightDir, zBufSun, buffer, ref landscape, point, width, height, clr);
            }
        }

        // определяем цвет каждой точки
        //private static Color CalcClr(List<Vector3> viewPolygon, Vector3 n, Vector3 v)
        //{

        //}

        /// <summary>
        /// Обработка модели с возможностью пропуска многоугольников с установленным полем special 
        /// Используется для создания теней: чтобы избежать собственных теней, земля пропускается.
        /// </summary>
        /// <param name="buffer">Используемый буфер</param>
        /// <param name="image">Картинка для вывода</param>
        /// <param name="m">Модель</param>
        /*private void ProcessModelForSun(int[][] buffer, Bitmap image, Model m)
        {
            Color draw;
            foreach (viewPolygon viewPolygon in m.polygons)
            {
                if (viewPolygon.ignore)
                    continue;
                viewPolygon.CalculatePointsInside(img.Width, img.Height);
                draw = viewPolygon.GetColor(sun);
                foreach (Point3D point in viewPolygon.pointsInside)
                {
                    ProcessPoint(buffer, image, point, draw);
                }
            }
        }*/

            /// <summary>
            /// Обработка одной точки и ее занесение в буфер
            /// </summary>
            /// <param name="point">Точка</param>
            /// <param name="color">Цвет точки</param>
        private static void ProcessPoint(Vector3 lightDir, float[][] zBufSun, float[][] buffer, ref Bitmap image, Vector3 point, int w, int h, Color clr)
        {
            Vector3 v1 = new Vector3(0, 0, 10);

            double angleBetween = AngleBetween(v1, lightDir);

            //PointTransform(ref point);
            int X = (int)Math.Round(point.X);
            int Y = (int)Math.Round(point.Y);
            int Z = (int)Math.Round(point.Z);

            if (!(X < 0 || X >= w || Y < 0 || Y >= h))
            {
                if (Z > buffer[Y][X])
                {
                    buffer[Y][X] = Z;
                    image.SetPixel(X, Y, clr);
                }

               // Vector3 sunPoint = Transformation.Transform(X, Y, Z, 0, 0, -90);

                //if (sunPoint.Z > zBufSun[Y][X])
                    //zBufSun[Y][X] = sunPoint.Z;
            }
        }

        private static Vector3 PointTransform(ref Vector3 point)
        {
            int k = 10;

            point.X = (k * point.X) / (point.Z + k);
            point.Y = (k * point.Y) / (point.Z + k);
            point.Z = (k * point.Z) / (point.Z + k);

            return point; 
        }

        static private void SetColor(float height, ref Color colorPix)
        {
            if (height > 235 && height <= 255)
                colorPix = Color.White;
            if (height > 180 && height <= 235)
                colorPix = Color.DarkGray;
            if (height > 150 && height <= 180)
                colorPix = Color.Gray;
            if (height <= 150)
                colorPix = Color.Green;
            if (height == 0)
                colorPix = Color.Blue;
        }

        private static Color Mix(Color a, Color b, float aPers)
        {
            aPers = Math.Min(aPers, 1);
            float bPers = 1 - aPers;
            int red = (int)(a.R * aPers + b.R * bPers);
            int green = (int)(a.G * aPers + b.G * bPers);
            int blue = (int)(a.B * aPers + b.B * bPers);

            return Color.FromArgb(red, green, blue);
        }

        private static double AngleBetween(Vector3 v1, Vector3 v2)
        {
            double cos = Vector3.Dot(v1, v2) / (v1.Length() * v2.Length());

            if (cos < 0)
                cos *= -1;

            return Math.Acos(cos);
        }
    }
}
