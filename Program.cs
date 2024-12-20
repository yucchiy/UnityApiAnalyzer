using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityApiAnalyzer;
using ZLogger;

var services = new ServiceCollection();

services.AddLogging(x =>
{
    x.ClearProviders();
    x.SetMinimumLevel(LogLevel.Trace);
    x.AddZLoggerConsole();
});

services.AddSingleton<Workspace>();
services.AddSingleton<UnityCsReferenceRepository>();
services.AddSingleton<ApiAnalyzer>();

ConsoleApp.ServiceProvider = services.BuildServiceProvider();

var app = ConsoleApp.Create();
app.Add<AnalyzeUnityApiCommand>();

app.Run(args);