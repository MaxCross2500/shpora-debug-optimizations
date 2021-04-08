using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Diagnostics;
using JPEG.Images;
using JPEG.Utilities;

namespace JPEG
{
	class Program
	{
		const int CompressionQuality = 70;

		static void Main(string[] args)
		{
			try
			{
				Console.WriteLine(IntPtr.Size == 8 ? "64-bit version" : "32-bit version");
				var sw = Stopwatch.StartNew();
				var fileName = @"sample.bmp";
//				var fileName = "Big_Black_River_Railroad_Bridge.bmp";
				var compressedFileName = fileName + ".compressed." + CompressionQuality;
				var uncompressedFileName = fileName + ".uncompressed." + CompressionQuality + ".bmp";
				
				using (var fileStream = File.OpenRead(fileName))
				using (var bmp = (Bitmap) Image.FromStream(fileStream, false, false))
				{
					sw.ReportAndRestart("{1}x{2} - {3:F2} MB loaded in {0}",
						bmp.Width, bmp.Height, fileStream.Length / (1024.0 * 1024));
					
					var imageMatrix = (Matrix) bmp;
					sw.ReportAndRestart("Converting to matrix");

					var compressionResult = Compress(imageMatrix, CompressionQuality);
					sw.ReportAndRestart("Compression: {0} ({1:F2} MB)",
						compressionResult.BitsCount / (float) (8 * 1024 * 1024));
					compressionResult.Save(compressedFileName);
					sw.ReportAndRestart("Saving compressed file");
				}

				var compressedImage = CompressedImage.Load(compressedFileName);
				sw.ReportAndRestart("Loading compressed file");
				
				var uncompressedImage = Uncompress(compressedImage);
				sw.ReportAndRestart("Decompression");
				
				var resultBmp = (Bitmap) uncompressedImage;
				sw.ReportAndRestart("Converting to bitmap");
				
				resultBmp.Save(uncompressedFileName, ImageFormat.Bmp);
				sw.ReportAndRestart("Saving decompressed image");
				Console.WriteLine();
				sw.Stop();
				
				Console.WriteLine($"Peak commit size: {MemoryMeter.PeakPrivateBytes() / (1024.0*1024):F2} MB");
				Console.WriteLine($"Peak working set: {MemoryMeter.PeakWorkingSet() / (1024.0*1024):F2} MB");
			}
			catch(Exception e)
			{
				Console.WriteLine(e);
			}
		}

		private static CompressedImage Compress(Matrix matrix, int quality = 50)
		{
			var dct = new DCT(DCTSize);
			
			var allQuantizedBytes = new byte[matrix.Height * matrix.Width * 3];

			var quantizationMatrix = GetQuantizationMatrix(quality);

			var subMatrix = new double[DCTSize, DCTSize];
			var channelFreqs = new double[DCTSize, DCTSize];
			var quantizedFreqs = new byte[DCTSize, DCTSize];

			var selectors = new Func<Pixel, double>[] {p => p.Y, p => p.Cb, p => p.Cr};

			for(var y = 0; y < matrix.Height / DCTSize; y++)
			for (var x = 0; x < matrix.Width / DCTSize; x++)
			for (var i = 0; i < selectors.Length; i++)
			{
				GetSubMatrix(matrix, y * DCTSize, DCTSize, x * DCTSize, DCTSize, selectors[i], subMatrix);
				ShiftMatrixValues(subMatrix, -128);
				dct.DCT2D(subMatrix, channelFreqs);
				Quantize(channelFreqs, quantizationMatrix, quantizedFreqs);
				// var oy = y / DCTSize;
				// var ox = x / DCTSize;
				// var offset = oy * matrix.Width / DCTSize * DCTSize * DCTSize * 3 + ox * DCTSize * DCTSize * 3 + i * DCTSize * DCTSize;
				var offset = y * DCTSize * matrix.Width * 3 + x * DCTSize * DCTSize * 3 + i * DCTSize * DCTSize;
				ZigZagScan(quantizedFreqs, allQuantizedBytes, offset);
			}

			var compressedBytes = HuffmanCodec.Encode(allQuantizedBytes, out var decodeTable, out var bitsCount);

			return new CompressedImage {Quality = quality, CompressedBytes = compressedBytes, BitsCount = bitsCount, DecodeTable = decodeTable, Height = matrix.Height, Width = matrix.Width};
		}
		
		private static Matrix Uncompress(CompressedImage image)
		{
			var dct = new DCT(DCTSize);

			var allQuantizedBytes = HuffmanCodec.Decode(image.CompressedBytes, image.DecodeTable, image.BitsCount);
			var quantizationMatrix = GetQuantizationMatrix(image.Quality);
			
			// var quantizedBytes = new byte[DCTSize * DCTSize];
			var quantizedFreqs = new byte[DCTSize, DCTSize];
			var channelFreqs = new double[DCTSize, DCTSize];
			
			var ys = new double[DCTSize, DCTSize];
			var cbs = new double[DCTSize, DCTSize];
			var crs = new double[DCTSize, DCTSize];

			var channels = new[] {ys, cbs, crs};
			
			var result = new Matrix(image.Height, image.Width);
			for (var y = 0; y < image.Height / DCTSize; y++)
			for (var x = 0; x < image.Width / DCTSize; x++)
			{
				for (var i = 0; i < channels.Length; i++)
				{
					var offset = y * DCTSize * image.Width * 3 + x * DCTSize * DCTSize * 3 + i * DCTSize * DCTSize;
					ZigZagUnScan(allQuantizedBytes, quantizedFreqs, offset);
					DeQuantize(quantizedFreqs, quantizationMatrix, channelFreqs);
					dct.IDCT2D(channelFreqs, channels[i]);
					ShiftMatrixValues(channels[i], 128);
				}

				SetPixelsYCbCr(result, ys, cbs, crs, y * DCTSize, x * DCTSize);
			}

			return result;
		}

		private static void ShiftMatrixValues(double[,] subMatrix, int shiftValue)
		{
			var height = subMatrix.GetLength(0);
			var width = subMatrix.GetLength(1);
			
			for (var y = 0; y < height; y++)
			for (var x = 0; x < width; x++)
				subMatrix[y, x] = subMatrix[y, x] + shiftValue;
		}

		private static void SetPixelsYCbCr(Matrix matrix, double[,] ys, double[,] cbs, double[,] crs, int yOffset, int xOffset)
		{
			var height = ys.GetLength(0);
			var width = ys.GetLength(1);

			for (var y = 0; y < height; y++)
			for (var x = 0; x < width; x++)
				matrix.Pixels[yOffset + y, xOffset + x] = Pixel.FromYCbCr(ys[y, x], cbs[y, x], crs[y, x]);
		}

		private static void GetSubMatrix(Matrix matrix, int yOffset, int yLength, int xOffset, int xLength,
			Func<Pixel, double> componentSelector, double[,] output)
		{
			for (var j = 0; j < yLength; j++)
			for (var i = 0; i < xLength; i++)
				output[j, i] = componentSelector(matrix.Pixels[yOffset + j, xOffset + i]);
		}
		
		private static readonly (int first, int second)[,] ZigZagScanTable = {
			{(0, 0), (0, 1), (1, 0), (2, 0), (1, 1), (0, 2), (0, 3), (1, 2)},
			{(2, 1), (3, 0), (4, 0), (3, 1), (2, 2), (1, 3), (0, 4), (0, 5)},
			{(1, 4), (2, 3), (3, 2), (4, 1), (5, 0), (6, 0), (5, 1), (4, 2)},
			{(3, 3), (2, 4), (1, 5), (0, 6), (0, 7), (1, 6), (2, 5), (3, 4)},
			{(4, 3), (5, 2), (6, 1), (7, 0), (7, 1), (6, 2), (5, 3), (4, 4)},
			{(5, 3), (2, 6), (1, 7), (2, 7), (3, 6), (4, 5), (5, 4), (6, 3)},
			{(7, 2), (7, 3), (6, 4), (5, 5), (4, 6), (3, 7), (4, 7), (5, 6)},
			{(6, 5), (7, 4), (7, 5), (6, 6), (5, 7), (6, 7), (7, 6), (7, 7)},
		};

		private static void ZigZagScan(byte[,] channelFreqs, byte[] quantizedBytes, int offset)
		{
			for (var y = 0; y < DCTSize; y++)
			for (var x = 0; x < DCTSize; x++)
				quantizedBytes[offset + y * DCTSize + x] = channelFreqs[ZigZagScanTable[y, x].first, ZigZagScanTable[y, x].second];
		}

		private static readonly int[,] ZigZagUnScanTable =
		{
			{0, 1, 5, 6, 14, 15, 27, 28},
			{2, 4, 7, 13, 16, 26, 29, 42},
			{3, 8, 12, 17, 25, 30, 41, 43},
			{9, 11, 18, 24, 31, 40, 44, 53},
			{10, 19, 23, 32, 39, 45, 52, 54},
			{20, 22, 33, 38, 46, 51, 55, 60},
			{21, 34, 37, 47, 50, 56, 59, 61},
			{35, 36, 48, 49, 57, 58, 62, 63},
		};

		private static void ZigZagUnScan(byte[] quantizedBytes, byte[,] quantizedFreqs, int offset)
		{
			for (var y = 0; y < DCTSize; y++)
			for (var x = 0; x < DCTSize; x++)
				quantizedFreqs[y, x] = quantizedBytes[offset + ZigZagUnScanTable[y, x]];
		}

		private static void Quantize(double[,] channelFreqs, int[,] quantizationMatrix, byte[,] output)
		{
			for (int y = 0; y < channelFreqs.GetLength(0); y++)
			for (int x = 0; x < channelFreqs.GetLength(1); x++)
				output[y, x] = (byte) (channelFreqs[y, x] / quantizationMatrix[y, x]);
		}

		private static void DeQuantize(byte[,] quantizedBytes, int[,] quantizationMatrix, double[,] output)
		{
			for (int y = 0; y < quantizedBytes.GetLength(0); y++)
			for (int x = 0; x < quantizedBytes.GetLength(1); x++)
				output[y, x] = ((sbyte) quantizedBytes[y, x]) * quantizationMatrix[y, x]; //NOTE cast to sbyte not to loose negative numbers
		}

		private static int[,] GetQuantizationMatrix(int quality)
		{
			if(quality < 1 || quality > 99)
				throw new ArgumentException("quality must be in [1,99] interval");

			var multiplier = quality < 50 ? 5000 / quality : 200 - 2 * quality;

			var result = new[,]
			{
				{16, 11, 10, 16, 24, 40, 51, 61},
				{12, 12, 14, 19, 26, 58, 60, 55},
				{14, 13, 16, 24, 40, 57, 69, 56},
				{14, 17, 22, 29, 51, 87, 80, 62},
				{18, 22, 37, 56, 68, 109, 103, 77},
				{24, 35, 55, 64, 81, 104, 113, 92},
				{49, 64, 78, 87, 103, 121, 120, 101},
				{72, 92, 95, 98, 112, 100, 103, 99}
			};

			for (int y = 0; y < result.GetLength(0); y++)
			for (int x = 0; x < result.GetLength(1); x++)
				result[y, x] = (multiplier * result[y, x] + 50) / 100;

			return result;
		}

		const int DCTSize = 8;
	}
}
