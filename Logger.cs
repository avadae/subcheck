using System;
using System.Collections.Generic;
using System.IO;

namespace SubCheck
{
	public static class Logger
	{
		private static readonly List<TextWriter> Writers = new List<TextWriter>();

		static Logger()
		{
			// By default, log to console
			Writers.Add(Console.Out);
		}

		public static void AddWriter(TextWriter writer)
		{
			if (writer != null && !Writers.Contains(writer))
				Writers.Add(writer);
		}

		public static void RemoveWriter(TextWriter writer)
		{
			if (writer != null && Writers.Contains(writer))
				Writers.Remove(writer);
		}

		public static void Write(string message)
		{
			foreach (var writer in Writers)
			{
				writer.Write(message);
				writer.Flush();
			}
		}

		public static void WriteLine(string message)
		{
			foreach (var writer in Writers)
			{
				writer.WriteLine(message);
				writer.Flush();
			}
		}

		public static void Close()
		{
			foreach (var writer in Writers)
			{
				writer.Flush();
				if (writer is IDisposable disposableWriter)
					disposableWriter.Dispose();
			}
			Writers.Clear();
		}
	}
}
