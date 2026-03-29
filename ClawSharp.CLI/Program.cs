using System.CommandLine;
using ClawSharp.CLI.Commands;
using ClawSharp.CLI.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = ServiceConfigurator.BuildHost(args);

var rootCommand = new RootCommand("ClawSharp CLI - Local-first AI application kernel");

rootCommand.AddCommand(InitCommand.Create(host));
rootCommand.AddCommand(ChatCommand.Create(host));
rootCommand.AddCommand(ListCommand.Create(host));
rootCommand.AddCommand(HistoryCommand.Create(host));
rootCommand.AddCommand(SpaceCommands.Create(host));
rootCommand.AddCommand(RegistryCommands.CreateAgents(host));
rootCommand.AddCommand(RegistryCommands.CreateSkills(host));

return await rootCommand.InvokeAsync(args);
