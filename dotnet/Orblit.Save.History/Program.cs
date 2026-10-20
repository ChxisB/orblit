using Orblit.Save;

Environment.ExitCode = await HistoryCli.RunAsync(args, Console.Out, Console.Error, () => DateTimeOffset.UtcNow);
