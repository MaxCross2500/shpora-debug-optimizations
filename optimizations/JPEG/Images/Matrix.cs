using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace JPEG.Images
{
    class Matrix
    {
        public readonly Pixel[,] Pixels;
        public readonly int Height;
        public readonly int Width;
				
        public Matrix(int height, int width)
        {
            Height = height;
            Width = width;
			
            Pixels = new Pixel[height,width];
            for(var i = 0; i< height; ++i)
            for(var j = 0; j< width; ++j)
                Pixels[i, j] = Pixel.FromRGB(0, 0, 0);
        }

        public static explicit operator Matrix(Bitmap bmp)
        {
            return FromBitmap(bmp);
            
            var height = bmp.Height - bmp.Height % 8;
            var width = bmp.Width - bmp.Width % 8;
            var matrix = new Matrix(height, width);

            for(var j = 0; j < height; j++)
            {
                for(var i = 0; i < width; i++)
                {
                    var pixel = bmp.GetPixel(i, j);
                    matrix.Pixels[j, i] = Pixel.FromRGB(pixel.R, pixel.G, pixel.B);
                }
            }

            return matrix;
        }
        
        public static explicit operator Bitmap(Matrix matrix)
        {
            return matrix.ToBitmap();
            
            var bmp = new Bitmap(matrix.Width, matrix.Height);

            for(var j = 0; j < bmp.Height; j++)
            for (var i = 0; i < bmp.Width; i++)
            {
                var pixel = matrix.Pixels[j, i];
                bmp.SetPixel(i, j, Color.FromArgb(pixel.R, pixel.G, pixel.B));
            }

            return bmp;
        }

        private static unsafe Matrix FromBitmap(Bitmap bitmap)
        {
            var height = bitmap.Height - bitmap.Height % 8;
            var width = bitmap.Width - bitmap.Width % 8;
            var matrix = new Matrix(height, width);

            var bmpData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);
            
            var pixelsData = (byte*) bmpData.Scan0;

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var curPtr = pixelsData + y * bmpData.Stride + x * 3;
                matrix.Pixels[y, x] = Pixel.FromRGB(curPtr[2], curPtr[1], curPtr[0]);
            }
            
            bitmap.UnlockBits(bmpData);

            return matrix;
        }

        private unsafe Bitmap ToBitmap()
        {
            var bitmap = new Bitmap(Width, Height);
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly,
                PixelFormat.Format24bppRgb);
            var pixelsData = (byte*) bmpData.Scan0;
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var curPtr = pixelsData + y * bmpData.Stride + x * 3;
                var pixel = Pixels[y, x];
                curPtr[2] = pixel.R;
                curPtr[1] = pixel.G;
                curPtr[0] = pixel.B;
            }
            bitmap.UnlockBits(bmpData);

            return bitmap;
        }

        public static byte ToByte(double d)
        {
            var val = (byte) d;
            return val;
        }
    }
}