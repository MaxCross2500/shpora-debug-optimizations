using System;
using JPEG.Utilities;

namespace JPEG
{
	public class DCT
	{
		private int dctSize;

		private double[,] xuCosTable;
		private double[,] yvCosTable;

		public DCT(int dctSize = 8)
		{
			this.dctSize = dctSize;

			xuCosTable = new double[dctSize, dctSize];
			yvCosTable = new double[dctSize, dctSize];

			MathEx.LoopByTwoVariables(0, dctSize, 0, dctSize, (x, u) =>
				xuCosTable[x, u] = Math.Cos(((2d * x + 1d) * u * Math.PI) / (2d * dctSize)));
			MathEx.LoopByTwoVariables(0, dctSize, 0, dctSize, (y, v) =>
				yvCosTable[y, v] = Math.Cos(((2d * y + 1d) * v * Math.PI) / (2d * dctSize)));
		}
		
		public double[,] DCT2D(double[,] input)
		{
			var height = input.GetLength(0);
			var width = input.GetLength(1);
			var coeffs = new double[width, height];

			MathEx.LoopByTwoVariables(
				0, width,
				0, height,
				(u, v) =>
				{
					var sum = MathEx
						.SumByTwoVariables(
							0, width,
							0, height,
							(x, y) => BasisFunction(input[x, y], u, v, x, y));

					coeffs[u, v] = sum * Beta(height, width) * Alpha(u) * Alpha(v);
				});
			
			return coeffs;
		}

		public void IDCT2D(double[,] coeffs, double[,] output)
		{
			for(var x = 0; x < coeffs.GetLength(1); x++)
			{
				for(var y = 0; y < coeffs.GetLength(0); y++)
				{
					var sum = MathEx
						.SumByTwoVariables(
							0, coeffs.GetLength(1),
							0, coeffs.GetLength(0),
							(u, v) => BasisFunction(coeffs[u, v], u, v, x, y) * Alpha(u) * Alpha(v));

					output[x, y] = sum * Beta(coeffs.GetLength(0), coeffs.GetLength(1));
				}
			}
		}

		public double BasisFunction(double a, int u, int v, int x, int y)
		{
			// var b = Math.Cos(((2d * x + 1d) * u * Math.PI) / (2 * width));
			// var c = Math.Cos(((2d * y + 1d) * v * Math.PI) / (2 * height));

			return a * xuCosTable[x, u] * yvCosTable[y, v];
		}

		private double Alpha(int u)
		{
			if (u == 0)
				return 0.7071067811865475;//1 / Math.Sqrt(2);
			return 1;
		}

		private double Beta(int height, int width)
		{
			return 1d / width + 1d / height;
		}
	}
}