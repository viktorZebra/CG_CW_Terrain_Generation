using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Numerics;
using System.Diagnostics;

namespace CG
{
    public partial class mainForm : Form
    {
        int SizeX = 100;
        int SizeY = 100;
        int countHills = 200;
        Bitmap landscape;
        int[][] faces;
        Vector3[] viewHeightmap;
        Vector3[] wordHeightmap;
        Color[] colorPix;
        public int height;
        public int width;
        int prevAngleX = 0;
        int prevAngleY = 0;
        int prevAngleZ = 0;
        double[][] heightmap;
        Vector3 lightDir = new Vector3(-1, 0, 0);
        int step = 1;

        public mainForm()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterScreen;
            Transformation.SetSize(scene.Width / 2, scene.Height / 2);
        }

        private void LoadFromFile(Vector3[] arr, Vector3[] wordHeightmap, Bitmap bmp, Color[] colorPix)
        {
            perlinPictureBox.Width = 300;
            perlinPictureBox.Height = 300;
            perlinPictureBox.Image = bmp;
            //bmp.RotateFlip(RotateFlipType.Rotate180FlipX);

            //Bitmap bmp1 = bmp.Clone(new Rectangle(0, 0, 300, 300), bmp.PixelFormat);
            //bmp1.RotateFlip(RotateFlipType.Rotate180FlipX);

            float coefX = 1.0f / bmp.Width;
            float coefY = 1.0f / bmp.Height;
            int count = 0;

            for (int i = 0; i < bmp.Height; i++)
            {
                for (int j = 0; j < bmp.Width; j++)
                {
                    wordHeightmap[count] = new Vector3(i * coefX, j * coefY, (bmp.GetPixel(j, i).R / 255f) * 100f);
                    wordHeightmap[count].X = (width * wordHeightmap[count].X);
                    wordHeightmap[count].Y = (height * wordHeightmap[count].Y);

                    SetColor(wordHeightmap[count].Z, ref colorPix[count]);

                    count++;

                }
            }
        }

        private void Scene_MouseWheel(object sender, MouseEventArgs e)
        {
            if (wordHeightmap != null)
            {
                if (e.Delta > 0)
                {
                    CG.Scale.ThreadScale(wordHeightmap, 1.1f, 1.1f, 1.1f, width / 2f, height / 2f);
                    Draw(lightDir, colorPix);
                }
                else
                {
                    CG.Scale.ThreadScale(wordHeightmap, 0.9f, 0.9f, 0.9f, width / 2f, height / 2f);
                    Draw(lightDir, colorPix);
                }
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }
        }

        public void Draw(Vector3 lightDir, Color[] colorPix)
        {
            Graphics g = Graphics.FromImage(landscape);
            g.Clear(Color.Black);

            Zbuffer.GenBuffer(width, height, ref landscape, faces, viewHeightmap, wordHeightmap, lightDir, colorPix);

            /*for (int i = 0; i < faces.Length; i++)
            {
                Point p1 = new Point((int)wordHeightmap[faces[i][0]].X, (int)wordHeightmap[faces[i][0]].Y);
                Point p2 = new Point((int)wordHeightmap[faces[i][1]].X, (int)wordHeightmap[faces[i][1]].Y);
                Point p3 = new Point((int)wordHeightmap[faces[i][2]].X, (int)wordHeightmap[faces[i][2]].Y);

                g.DrawPolygon(new Pen(Color.Black), new Point[] { p1, p2, p3 });
            }*/

            scene.Image = landscape;
        }

        public void convert(Vector3[] arr, Vector3[] wordHeightmap, double[][] heightmap, Color[] colorPix)
        {
            int buf = SizeX;
            float coef = 1.0f / buf;
            int count = 0;
            for (int i = 0; i < heightmap.Length; i++)
            {
                for (int j = 0; j < heightmap[0].Length; j++)
                {
                    wordHeightmap[count] = new Vector3(i * coef, j * coef, (float)heightmap[i][j]);
                    wordHeightmap[count].X = (width * wordHeightmap[count].X);
                    wordHeightmap[count].Y = (height * wordHeightmap[count].Y);
                    wordHeightmap[count].Z = (255 * wordHeightmap[count].Z);

                    SetColor(wordHeightmap[count].Z, ref colorPix[count]);

                    arr[count] = wordHeightmap[count];

                    count++;
                }
            }
        }

        private void UpMovebutton_Click(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                CG.Move.ThreadMove(wordHeightmap, 0, -10, 0);
                Draw(lightDir, colorPix);
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }
        }

        private void DownMovebutton_Click(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                CG.Move.ThreadMove(wordHeightmap, 0, 10, 0);
                Draw(lightDir, colorPix);
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }
        }

        private void LeftMovebutton_Click(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                CG.Move.ThreadMove(wordHeightmap, -10, 0, 0);
                Draw(lightDir, colorPix);
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }
        }

        private void RightMovebutton_Click(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                CG.Move.ThreadMove(wordHeightmap, 10, 0, 0);
                Draw(lightDir, colorPix);
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }
        }

        private void rotXtrackBar_Scroll(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                int angle = rotXtrackBar.Value;
                angle -= prevAngleX;
                prevAngleX = rotXtrackBar.Value;

                Rotate.ThreadRotate(wordHeightmap, angle, 1, width / 2f, height / 2f);
                Draw(lightDir, colorPix);
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }
        }

        private void rotYtrackBar_Scroll(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                int angle = rotYtrackBar.Value;
                angle -= prevAngleY;
                prevAngleY = rotYtrackBar.Value;

                Rotate.ThreadRotate(wordHeightmap, angle, 2, width / 2f, height / 2f);
                Draw(lightDir, colorPix);
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }
        }

        private void rotZtrackBar_Scroll(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                int angle = rotZtrackBar.Value;
                angle -= prevAngleZ;
                prevAngleZ = rotZtrackBar.Value;

                Rotate.ThreadRotate(wordHeightmap, angle, 3, width / 2f, height / 2f);
                Draw(lightDir, colorPix);
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }
        }

        private void changeLightbutton_Click(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                try
                {
                    int x = int.Parse(xLighttextBox.Text);
                    int y = int.Parse(yLighttextBox.Text);
                    int z = int.Parse(zLighttextBox.Text);

                    lightDir = new Vector3(x, y, z);

                    Draw(lightDir, colorPix);
                }
                catch
                {
                    MessageBox.Show("Вводить только целые числа", "Ошибка", MessageBoxButtons.OK);
                }
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }
        }

        private void GenPerlin(Vector3[] arr, Vector3[] wordHeightmap, Color[] colorPix)
        {
            perlinPictureBox.Width = 300;
            perlinPictureBox.Height = 300;

            Perlin.CreateBitmapAtRuntime(1, perlinPictureBox.Width, perlinPictureBox.Height, perlinPictureBox);
            Bitmap bmp = (Bitmap)perlinPictureBox.Image;

            float coefX = 1.0f / bmp.Width;
            float coefY = 1.0f / bmp.Height;
            int count = 0;

            for (int i = 0; i < bmp.Height; i++)
            {
                for (int j = 0; j < bmp.Width; j++)
                {
                    wordHeightmap[count] = new Vector3(i * coefX, j * coefY, (bmp.GetPixel(j, i).R / 255f) * 100f);
                    wordHeightmap[count].X = (width * wordHeightmap[count].X);
                    wordHeightmap[count].Y = (height * wordHeightmap[count].Y);

                    SetColor(wordHeightmap[count].Z, ref colorPix[count]);

                    count++;

                }
            }
        }

        private void загрузитьКартуВысотToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            rotXtrackBar.Value = 0;
            rotYtrackBar.Value = 0;
            rotZtrackBar.Value = 0;

            prevAngleX = 0;
            prevAngleY = 0;
            prevAngleZ = 0;


            OpenFileDialog opfd = new OpenFileDialog() { Title = "Загрузить карту высот", Filter = "Bitmap|*.bmp" };

            //Если выбрали то
            if (opfd.ShowDialog(this) == DialogResult.OK)
            {
                Bitmap bmp = (Bitmap)Bitmap.FromFile(opfd.FileName);

                viewHeightmap = new Vector3[bmp.Width * bmp.Height];
                wordHeightmap = new Vector3[bmp.Width * bmp.Height];
                colorPix = new Color[bmp.Width * bmp.Height];

                LoadFromFile(viewHeightmap, wordHeightmap, bmp, colorPix);

                faces = GenerateHeightmap.Faces(bmp.Width, bmp.Height, step);

                Draw(lightDir, colorPix);
            }
        }

        private void шумПерлинаToolStripMenuItem_Click(object sender, EventArgs e)
        {
            rotXtrackBar.Value = 0;
            rotYtrackBar.Value = 0;
            rotZtrackBar.Value = 0;

            prevAngleX = 0;
            prevAngleY = 0;
            prevAngleZ = 0;

            viewHeightmap = new Vector3[300 * 300];
            wordHeightmap = new Vector3[300 * 300];
            colorPix = new Color[300 * 300];

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            GenPerlin(viewHeightmap, wordHeightmap, colorPix);
            stopWatch.Stop();
            long ts = stopWatch.ElapsedMilliseconds;
            StreamWriter sw = new StreamWriter("time.txt", true);
            sw.WriteLine(string.Format("{0} {1}", SizeX, ts));
            sw.Close();

            faces = GenerateHeightmap.Faces(300, 300, step);

            Draw(lightDir, colorPix);

        }

        private void холмовойАлгоритмToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                perlinPictureBox.Image = null;
                rotXtrackBar.Value = 0;
                rotYtrackBar.Value = 0;
                rotZtrackBar.Value = 0;

                prevAngleX = 0;
                prevAngleY = 0;
                prevAngleZ = 0;

                if (sizeHillsTextBox.Text != "")
                {
                    SizeX = int.Parse(sizeHillsTextBox.Text);
                    SizeY = SizeX;
                }

                if (countHillsTextBox.Text != "")
                {
                    countHills = int.Parse(countHillsTextBox.Text);
                }

                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                heightmap = GenerateHeightmap.Hills(SizeX, SizeY, smoothCheckBox.Checked, valleyCheckBox.Checked, countHills);
                faces = GenerateHeightmap.Faces(SizeX, SizeY, step);
                stopWatch.Stop();
                long ts = stopWatch.ElapsedMilliseconds;
                StreamWriter sw = new StreamWriter("time.txt", true);
                sw.WriteLine(string.Format("{0} {1}", SizeX, ts));
                sw.Close();

                viewHeightmap = new Vector3[SizeX * SizeY];
                wordHeightmap = new Vector3[SizeX * SizeY];
                colorPix = new Color[SizeX * SizeY];

                convert(viewHeightmap, wordHeightmap, heightmap, colorPix);

                Draw(lightDir, colorPix);

            }
            catch
            {
                MessageBox.Show("Вводить только целые числа", "Ошибка", MessageBoxButtons.OK);
            }

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
            if (height <= 0)
                colorPix = Color.Blue;
        }

        private void scene_DoubleClick(object sender, EventArgs e)
        {
            var sfd = new SaveFileDialog() { Title = "Сохранение 3D изображения", Filter = "Image|*.png" };
            if (sfd.ShowDialog() == DialogResult.OK)
                scene.Image.Save(sfd.FileName);
        }

        private void оПрограммеToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string about = "\tДанная программа предназначена для 3D визуализации ландшафта по карте высот." +
                "\n\tКарту высот можно загрузить самому, выбрав соответсвующий пункт в меню Генерации ландшафта. Изображение не должно превышать 300х300 пикселей, формат - BMP." +
                "\n\tТакже есть возможность сгенерировать карту высот двумя алгоритмами - холмовой и шум Перлина." +
                "\n\tДля холмового алгоритма можно задать размер карты и кол-во холмов. (по умолчанию: размер - 100х100, кол-во холмов - 200)" +
                "\n\tСправа находятся виджеты для управления ландшафтом. Стрелочками его можно переместить, трекбарами - повернуть. При наведении курсора мыши на сцену, колесиком изменяется мастштаб изображения." +
                "\n\tМожно задать вектор направления света. (по умолчанию: (1, -1, -1))." +
                "\n\tРезультирующее изображение можно сохранить двойным кликом по сцене.";
            MessageBox.Show(about, "О программе", MessageBoxButtons.OK);
        }

        private void mainForm_Load(object sender, EventArgs e)
        {
            scene.MouseWheel += Scene_MouseWheel;
            height = scene.Height;
            width = scene.Width;
            landscape = new Bitmap(width, height);
            FormBorderStyle = FormBorderStyle.Sizable;

        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                int angle = 90;

                Rotate.RotatePoints(wordHeightmap, angle, 1, width / 2f, height / 2f);
                Draw(lightDir, colorPix);
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                int angle = -90;

                Rotate.RotatePoints(wordHeightmap, angle, 1, width / 2f, height / 2f);
                Draw(lightDir, colorPix);
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }

        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                int angle = 90;

                Rotate.RotatePoints(wordHeightmap, angle, 3, width / 2f, height / 2f);
                Draw(lightDir, colorPix);
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }

        }

        private void button5_Click(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                int angle = -90;

                Rotate.RotatePoints(wordHeightmap, angle, 3, width / 2f, height / 2f);
                Draw(lightDir, colorPix);
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                int angle = 90;

                Rotate.RotatePoints(wordHeightmap, angle, 2, width / 2f, height / 2f);
                Draw(lightDir, colorPix);
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (wordHeightmap != null)
            {
                int angle = -90;

                Rotate.RotatePoints(wordHeightmap, angle, 2, width / 2f, height / 2f);
                Draw(lightDir, colorPix);
            }
            else
            {
                MessageBox.Show("Картa высот не загружена или не сгенерирована", "Ошибка", MessageBoxButtons.OK);
            }
        }
    }
}
