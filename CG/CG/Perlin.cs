using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SimplexNoise;

namespace CG
{
    /// <summary>
    /// Implementation of the Perlin simplex noise, an improved Perlin noise algorithm.
    /// Based loosely on SimplexNoise1234 by Stefan Gustavson <http://staffwww.itn.liu.se/~stegu/aqsis/aqsis-newnoise/>
    /// 
    /// </summary>
    static public class Perlin
    {
        static private byte[] byteBackup;

        static public byte[] CreateBitmapAtRuntime(int gridSize, int width, int height, PictureBox perlinPictureBox)
        {
            byteBackup = new byte[Noise.perm.Length];
            Noise.perm.CopyTo(byteBackup, 0);


            Bitmap flag = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            Rectangle flagRect = new Rectangle(0, 0, width, height);
            Graphics flagGraphics = Graphics.FromImage(flag);

            System.Drawing.Imaging.BitmapData flagData = flag.LockBits(flagRect, System.Drawing.Imaging.ImageLockMode.ReadWrite, flag.PixelFormat);
            int imageByteSize = flagData.Stride * flagData.Height;
            byte[] destinationData = new byte[imageByteSize];
            int bitsPerPixelElement = 32 / 8;

            byte[] noiseSeed;
            noiseSeed = byteBackup;


            Noise.perm = noiseSeed;

            for (int x = 0; x < width; x += gridSize)
            {
                for (int y = 0; y < height; y += gridSize)
                {
                    byte cval = (byte)(Noise.Generate(x / 150f, y / 150f) * 128 + 128);

                    for (int i = 0; i < gridSize; i++)
                    {
                        if ((x + i) >= width) break;
                        for (int j = 0; j < gridSize; j++)
                        {
                            if ((y + j) >= height) break;
                            destinationData[(y + j) * flagData.Stride + (x + i) * bitsPerPixelElement + 2] = (byte)((float)cval);// * 0.6); // R
                            destinationData[(y + j) * flagData.Stride + (x + i) * bitsPerPixelElement + 1] = (byte)((float)cval);// * 0.6); // G
                            destinationData[(y + j) * flagData.Stride + (x + i) * bitsPerPixelElement] = cval; // B
                        }
                    }

                }
            }

            System.Runtime.InteropServices.Marshal.Copy(destinationData, 0, flagData.Scan0, destinationData.Length);
            flag.UnlockBits(flagData);

            perlinPictureBox.Image = flag;
            perlinPictureBox.Invalidate();

            return destinationData;
        }

    }
}