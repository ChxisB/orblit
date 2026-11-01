using System.Text;
using Orblit.Remote;

// The console's own encoding is the system's on some platforms, and messages are UTF-8.
var utf8 = new UTF8Encoding(false);
using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { NewLine = "\n", AutoFlush = true };
using var error = new StreamWriter(Console.OpenStandardError(), utf8) { NewLine = "\n", AutoFlush = true };
var input = RemoteCli.OpenInput(Console.OpenStandardInput());

Environment.ExitCode = await RemoteCli.RunAsync(
    args,
    new RemoteCliEnvironment(input, output, error, Environment.GetEnvironmentVariable));
