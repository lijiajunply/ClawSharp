using System.CommandLine;
using ClawSharp.CLI.Commands;
using ClawSharp.CLI.Infrastructure;

// 0. 预初始化本地化，确保 Bootstrap 阶段也使用正确语言
I18n.InitializeForBootstrap();

// 1. 引导检查 (Bootstrap Check)
if (!await BootstrapWizard.RunAsync())
{
    return 1;
}

// 2. 构建宿主 (Build Host)
var host = ServiceConfigurator.BuildHost(args);

// 3. 初始化本地化 (Initialize Localization)
I18n.Initialize(host);

var rootCommand = new RootCommand(I18n.T("RootCommand.Description"));

rootCommand.AddCommand(InitCommand.Create(host));
rootCommand.AddCommand(ChatCommand.Create(host));
rootCommand.AddCommand(ListCommand.Create(host));
rootCommand.AddCommand(HistoryCommand.Create(host));
rootCommand.AddCommand(StatsCommands.Create(host));
rootCommand.AddCommand(SpaceCommands.Create(host));
rootCommand.AddCommand(McpCommands.Create(host));
rootCommand.AddCommand(ConfigCommands.Create(host));
rootCommand.AddCommand(RegistryCommands.CreateAgents(host));
rootCommand.AddCommand(RegistryCommands.CreateSkills(host));
rootCommand.AddCommand(HubCommands.Create(host));

rootCommand.SetHandler(async () =>
{
    await ChatCommand.RunAsync(host, null);
});

return await rootCommand.InvokeAsync(args);
