using System;
using System.IO;

namespace UltrawideToys.Core;

public static class LocalLog
{
	private const long MaxBytes = 1048576L;

	private const int BackupCount = 3;

	private static readonly object Gate = new object();

	public static void Info(string message)
	{
		Write("INFO", message);
	}

	public static void Error(string message, Exception? exception = null)
	{
		Write("ERROR", (exception == null) ? message : $"{message}: {exception}");
	}

	private static void Write(string level, string message)
	{
		try
		{
			lock (Gate)
			{
				string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UltrawideMonitor", "logs");
				Directory.CreateDirectory(directory);
				string path = Path.Combine(directory, "ultrawidemonitor.log");
				if (File.Exists(path) && new FileInfo(path).Length >= 1048576)
				{
					Rotate(path);
				}
				File.AppendAllText(path, $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
			}
		}
		catch
		{
		}
	}

	private static void Rotate(string path)
	{
		for (int index = 2; index >= 1; index--)
		{
			string source = $"{path}.{index}";
			string destination = $"{path}.{index + 1}";
			if (File.Exists(source))
			{
				File.Move(source, destination, overwrite: true);
			}
		}
		if (File.Exists(path))
		{
			File.Move(path, path + ".1", overwrite: true);
		}
	}
}

