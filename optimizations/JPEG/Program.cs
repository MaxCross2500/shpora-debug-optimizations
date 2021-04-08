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

			for(var y = 0; y < matrix.Height; y += DCTSize)
			for (var x = 0; x < matrix.Width; x += DCTSize)
			for (var i = 0; i < selectors.Length; i++)
			{
				GetSubMatrix(matrix, y, DCTSize, x, DCTSize, selectors[i], subMatrix);
				ShiftMatrixValues(subMatrix, -128);
				dct.DCT2D(subMatrix, channelFreqs);
				Quantize(channelFreqs, quantizationMatrix, quantizedFreqs);
				// var oy = y / DCTSize;
				// var ox = x / DCTSize;
				// var offset = oy * matrix.Width / DCTSize * DCTSize * DCTSize * 3 + ox * DCTSize * DCTSize * 3 + i * DCTSize * DCTSize;
				var offset = y * matrix.Width * 3 + x * DCTSize * 3 + i * DCTSize * DCTSize;
				ZigZagScan(quantizedFreqs, allQuantizedBytes, offset);
			}

			var compressedBytes = HuffmanCodec.Encode(allQuantizedBytes, out var decodeTable, out var bitsCount);

			return new CompressedImage {Quality = quality, CompressedBytes = compressedBytes, BitsCount = bitsCount, DecodeTable = decodeTable, Height = matrix.Height, Width = matrix.Width};
		}
		
		private static Matrix Uncompress(CompressedImage image)
		{
			var dct = new DCT(DCTSize);
			
			var quantizationMatrix = GetQuantizationMatrix(image.Quality);
			
			var quantizedBytes = new byte[DCTSize * DCTSize];
			var channelFreqs = new double[DCTSize, DCTSize];
			
			var ys = new double[DCTSize, DCTSize];
			var cbs = new double[DCTSize, DCTSize];
			var crs = new double[DCTSize, DCTSize];
			
			var result = new Matrix(image.Height, image.Width);
			using (var allQuantizedBytes =
				new MemoryStream(HuffmanCodec.Decode(image.CompressedBytes, image.DecodeTable, image.BitsCount)))
			{
				for (var y = 0; y < image.Height; y += DCTSize)
				for (var x = 0; x < image.Width; x += DCTSize)
				{
					foreach (var channel in new[] {ys, cbs, crs})
					{
						allQuantizedBytes.ReadAsync(quantizedBytes, 0, quantizedBytes.Length).Wait();
						var quantizedFreqs = ZigZagUnScan(quantizedBytes);
						DeQuantize(quantizedFreqs, quantizationMatrix, channelFreqs);
						dct.IDCT2D(channelFreqs, channel);
						ShiftMatrixValues(channel, 128);
					}

					SetPixelsYCbCr(result, ys, cbs, crs, y, x);
				}
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

		private static byte[,] ZigZagUnScan(IReadOnlyList<byte> quantizedBytes)
		{
			return new[,]
			{
				{ quantizedBytes[0], quantizedBytes[1], quantizedBytes[5], quantizedBytes[6], quantizedBytes[14], quantizedBytes[15], quantizedBytes[27], quantizedBytes[28] },
				{ quantizedBytes[2], quantizedBytes[4], quantizedBytes[7], quantizedBytes[13], quantizedBytes[16], quantizedBytes[26], quantizedBytes[29], quantizedBytes[42] },
				{ quantizedBytes[3], quantizedBytes[8], quantizedBytes[12], quantizedBytes[17], quantizedBytes[25], quantizedBytes[30], quantizedBytes[41], quantizedBytes[43] },
				{ quantizedBytes[9], quantizedBytes[11], quantizedBytes[18], quantizedBytes[24], quantizedBytes[31], quantizedBytes[40], quantizedBytes[44], quantizedBytes[53] },
				{ quantizedBytes[10], quantizedBytes[19], quantizedBytes[23], quantizedBytes[32], quantizedBytes[39], quantizedBytes[45], quantizedBytes[52], quantizedBytes[54] },
				{ quantizedBytes[20], quantizedBytes[22], quantizedBytes[33], quantizedBytes[38], quantizedBytes[46], quantizedBytes[51], quantizedBytes[55], quantizedBytes[60] },
				{ quantizedBytes[21], quantizedBytes[34], quantizedBytes[37], quantizedBytes[47], quantizedBytes[50], quantizedBytes[56], quantizedBytes[59], quantizedBytes[61] },
				{ quantizedBytes[35], quantizedBytes[36], quantizedBytes[48], quantizedBytes[49], quantizedBytes[57], quantizedBytes[58], quantizedBytes[62], quantizedBytes[63] }
			};
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
