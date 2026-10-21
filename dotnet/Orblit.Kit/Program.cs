using Orblit.Asset;

Environment.ExitCode = await KitCli.RunAsync(
    args, new KitCliEnvironment(Console.Out, Console.Error, () => DateTime.UtcNow, () => new HttpTransport()));
