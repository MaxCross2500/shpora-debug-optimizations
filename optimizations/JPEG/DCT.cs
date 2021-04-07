using System;
using JPEG.Utilities;

namespace JPEG
{
	public class DCT
	{
		private int dctSize;
		private double beta;

		private double[,] xuCosTable;
		private double[,] yvCosTable;

		public DCT(int dctSize = 8)
		{
			this.dctSize = dctSize;
			beta = 1d / dctSize + 1d / dctSize;

			xuCosTable = new double[dctSize, dctSize];
			yvCosTable = new double[dctSize, dctSize];

			MathEx.LoopByTwoVariables(0, dctSize, 0, dctSize, (x, u) =>
				xuCosTable[x, u] = Math.Cos(((2d * x + 1d) * u * Math.PI) / (2d * dctSize)));
			MathEx.LoopByTwoVariables(0, dctSize, 0, dctSize, (y, v) =>
				yvCosTable[y, v] = Math.Cos(((2d * y + 1d) * v * Math.PI) / (2d * dctSize)));
		}
		
		public double[,] DCT2D(double[,] input)
		{
			var coeffs = new double[dctSize, dctSize];

			for(var u = 0; u < dctSize; u++)
			for (var v = 0; v < dctSize; v++)
			{
				var sum = 0d;
				for (var x = 0; x < dctSize; x++)
				for (var y = 0; y < dctSize; y++)
					sum += BasisFunction(input[x, y], u, v, x, y);

				coeffs[u, v] = sum * beta * Alpha(u) * Alpha(v);
			}

			return coeffs;
		}

		public void IDCT2D(double[,] coeffs, double[,] output)
		{
			for(var x = 0; x < dctSize; x++)
			for (var y = 0; y < dctSize; y++)
			{
				var sum = 0d;
				for (var u = 0; u < dctSize; u++)
				for (var v = 0; v < dctSize; v++)
					sum += BasisFunction(coeffs[u, v], u, v, x, y) * Alpha(u) * Alpha(v);

				output[x, y] = sum * beta;
			}
		}

		public double BasisFunction(double a, int u, int v, int x, int y) => a * xuCosTable[x, u] * yvCosTable[y, v];

		private double Alpha(int u) => u == 0 ? 0.7071067811865475 : 1;
	}
}