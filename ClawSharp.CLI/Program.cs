using System.CommandLine;
using ClawSharp.CLI.Commands;
using ClawSharp.CLI.Infrastructure;

// 1. 引导检查 (Bootstrap Check)
if (!await BootstrapWizard.RunAsync())
{
    return 1;
}

// 2. 构建宿主 (Build Host)
var host = ServiceConfigurator.BuildHost(args);

var rootCommand = new RootCommand("ClawSharp CLI - Local-first AI application kernel");

rootCommand.AddCommand(InitCommand.Create(host));
rootCommand.AddCommand(ChatCommand.Create(host));
rootCommand.AddCommand(ListCommand.Create(host));
rootCommand.AddCommand(HistoryCommand.Create(host));
rootCommand.AddCommand(StatsCommands.Create(host));
rootCommand.AddCommand(SpaceCommands.Create(host));
rootCommand.AddCommand(ConfigCommands.Create(host));
rootCommand.AddCommand(RegistryCommands.CreateAgents(host));
rootCommand.AddCommand(RegistryCommands.CreateSkills(host));

rootCommand.SetHandler(async () =>
{
    await ChatCommand.RunAsync(host, null);
});

return await rootCommand.InvokeAsync(args);
