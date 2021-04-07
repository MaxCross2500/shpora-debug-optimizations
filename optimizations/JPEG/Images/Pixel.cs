using System;
using System.Linq;

namespace JPEG.Images
{
    public struct Pixel
    {
        private readonly PixelFormat format;

        public Pixel(double firstComponent, double secondComponent, double thirdComponent, PixelFormat pixelFormat)
        {
            if (!new[]{PixelFormat.RGB, PixelFormat.YCbCr}.Contains(pixelFormat))
                throw new FormatException("Unknown pixel format: " + pixelFormat);
            format = pixelFormat;
            c0 = firstComponent;
            c1 = secondComponent;
            c2 = thirdComponent;
        }

        private readonly double c0;
        private readonly double c1;
        private readonly double c2;

        public double R => format == PixelFormat.RGB ? c0 : (298.082 * c0 + 408.583 * c2) / 256.0 - 222.921;
        public double G => format == PixelFormat.RGB ? c1 : (298.082 * c0 - 100.291 * c1 - 208.120 * c2) / 256.0 + 135.576;
        public double B => format == PixelFormat.RGB ? c2 : (298.082 * c0 + 516.412 * c1) / 256.0 - 276.836;

        public double Y => format == PixelFormat.YCbCr ? c0 : 16.0 + (65.738 * c0 + 129.057 * c1 + 24.064 * c2) / 256.0;
        public double Cb => format == PixelFormat.YCbCr ? c1 : 128.0 + (-37.945 * c0 - 74.494 * c1 + 112.439 * c2) / 256.0;
        public double Cr => format == PixelFormat.YCbCr ? c2 : 128.0 + (112.439 * c0 - 94.154 * c1 - 18.285 * c2) / 256.0;
    }
}