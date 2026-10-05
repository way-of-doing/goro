using Goro.Cli;

return await new GoroApp(GoroApp.DefaultServices()).RunAsync(args, CancellationToken.None);
