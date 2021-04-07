using System;
using System.Linq;

namespace JPEG.Images
{
    public struct Pixel
    {
        public static Pixel FromRGB(byte r, byte g, byte b) => new Pixel {R = r, G = g, B = b};
        public static Pixel FromYCbCr(double y, double cb, double cr) =>
            new Pixel
            {
                R = Matrix.ToByte((298.082 * y + 408.583 * cr) / 256.0 - 222.921),
                G = Matrix.ToByte((298.082 * y - 100.291 * cb - 208.120 * cr) / 256.0 + 135.576),
                B = Matrix.ToByte((298.082 * y + 516.412 * cb) / 256.0 - 276.836)
            };
        

        public byte R;
        public byte G;
        public byte B;

        public double Y => 16.0 + (65.738 * R + 129.057 * G + 24.064 * B) / 256.0;
        public double Cb => 128.0 + (-37.945 * R - 74.494 * G + 112.439 * B) / 256.0;
        public double Cr => 128.0 + (112.439 * R - 94.154 * G - 18.285 * B) / 256.0;
    }
}