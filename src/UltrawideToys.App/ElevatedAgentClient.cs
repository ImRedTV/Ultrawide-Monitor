using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace UltrawideToys;

internal static class ElevatedAgentClient
{
	public static async Task SendAsync(string command)
	{
		await using NamedPipeClientStream pipe = new NamedPipeClientStream(".", "UltrawideToys.Elevated", PipeDirection.Out, PipeOptions.Asynchronous);
		await pipe.ConnectAsync(250).ConfigureAwait(continueOnCapturedContext: false);
		await using StreamWriter writer = new StreamWriter(pipe)
		{
			AutoFlush = true
		};
		await writer.WriteLineAsync(command).ConfigureAwait(continueOnCapturedContext: false);
	}
}

