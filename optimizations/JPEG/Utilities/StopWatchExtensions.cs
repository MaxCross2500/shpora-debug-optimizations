using System;
using System.Diagnostics;
using System.Linq;

namespace JPEG.Utilities
{
	public static class StopWatchExtensions
	{
		public static void ReportAndRestart(this Stopwatch stopwatch, string text)
		{
			stopwatch.Stop();
			Console.WriteLine($"{text}: {stopwatch.Elapsed}");
			stopwatch.Restart();
		}
		
		public static void ReportAndRestart(this Stopwatch stopwatch, string text, params object[] args)
		{
			stopwatch.Stop();
			Console.WriteLine(text, args.Prepend(stopwatch.Elapsed).ToArray());
			stopwatch.Restart();
		}
	}
}