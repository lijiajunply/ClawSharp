using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class McpCommands
{
    public static Command Create(IHost host)
    {
        var command = new Command("mcp", I18n.T("Mcp.Description"));
        command.AddCommand(CreateList(host));
        command.AddCommand(CreateSearch(host));
        command.AddCommand(CreateShow(host));
        command.AddCommand(CreateInstall(host));
        return command;
    }

    private static Command CreateInstall(IHost host)
    {
        var command = new Command("install", I18n.T("Mcp.Install.Description"));
        var nameArg = new Argument<string>("name", I18n.T("Mcp.Install.NameArg"));
        command.AddArgument(nameArg);

        command.SetHandler(async name =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var smithery = host.Services.GetRequiredService<ISmitheryClient>();
                var installer = host.Services.GetRequiredService<IMcpInstaller>();

                var server = await smithery.GetServerAsync(name);

                if (server.McpConfig == null)
                {
                    AnsiConsole.MarkupLine(I18n.T("Mcp.NoAutomatedConfig"));
                    return 0;
                }

                AnsiConsole.MarkupLine(I18n.T("Mcp.Installing", server.QualifiedName.EscapeMarkup()));

                var envVars = new Dictionary<string, string>();
                if (server.McpConfig.Env?.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine(I18n.T("Mcp.ConfigRequired"));
                    foreach (var env in server.McpConfig.Env)
                    {
                        var prompt = string.IsNullOrWhiteSpace(env.Value.Description)
                            ? I18n.T("Mcp.EnvPrompt", env.Key.EscapeMarkup())
                            : I18n.T("Mcp.EnvPromptWithDescription", env.Key.EscapeMarkup(), env.Value.Description.EscapeMarkup());
                        
                        string val;
                        if (env.Key.Contains("KEY") || env.Key.Contains("TOKEN") || env.Key.Contains("SECRET"))
                        {
                            val = AnsiConsole.Prompt(new TextPrompt<string>(prompt).PromptStyle("grey").Secret());
                        }
                        else
                        {
                            val = AnsiConsole.Ask<string>(prompt);
                        }
                        envVars[env.Key] = val;
                    }
                }

                await installer.InstallAsync(server.Name, server.McpConfig.Command, server.McpConfig.Args, envVars);
                
                AnsiConsole.MarkupLine(I18n.T("Mcp.Install.Success", server.Name.EscapeMarkup()));
                AnsiConsole.MarkupLine(I18n.T("Mcp.Install.ConfigPath"));
                AnsiConsole.MarkupLine(I18n.T("Mcp.Install.Reload"));

                return 0;
            });
        }, nameArg);

        return command;
    }

    private static Command CreateList(IHost host)
    {
        var command = new Command("list", I18n.T("Mcp.List.Description"));
        command.SetHandler(async () =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var catalog = host.Services.GetRequiredService<IMcpServerCatalog>();
                var servers = catalog.GetAll();

                if (servers.Count == 0)
                {
                    AnsiConsole.MarkupLine(I18n.T("Mcp.List.Empty"));
                    return 0;
                }

                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn(I18n.T("Common.Name"));
                table.AddColumn(I18n.T("Common.Command"));
                table.AddColumn(I18n.T("Mcp.List.Column.Arguments"));
                table.AddColumn(I18n.T("Chat.Mcp.Column.Capabilities"));

                foreach (var server in servers)
                {
                    table.AddRow(
                        server.Name.EscapeMarkup(),
                        server.Command.EscapeMarkup(),
                        server.Arguments.EscapeMarkup(),
                        server.Capabilities.ToString());
                }

                AnsiConsole.Write(table);
                return 0;
            });
        });
        return command;
    }

    private static Command CreateSearch(IHost host)
    {
        var command = new Command("search", I18n.T("Mcp.Search.Description"));
        var queryArg = new Argument<string?>("query", () => null, I18n.T("Mcp.Search.QueryArg"));
        command.AddArgument(queryArg);

        command.SetHandler(async query =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var smithery = host.Services.GetRequiredService<ISmitheryClient>();
                
                await AnsiConsole.Status()
                    .StartAsync(I18n.T("Mcp.Searching"), async ctx =>
                    {
                        var results = await smithery.SearchServersAsync(query);

                        if (results.Count == 0)
                        {
                            AnsiConsole.MarkupLine(I18n.T("Mcp.Search.Empty"));
                            return;
                        }

                        var table = new Table().Border(TableBorder.Rounded).Expand();
                        table.AddColumn(I18n.T("Chat.Mcp.Column.QualifiedName"));
                        table.AddColumn(I18n.T("Common.Description"));
                        table.AddColumn(I18n.T("Common.Author"));
                        table.AddColumn(I18n.T("Mcp.Search.Column.Downloads"));
                        table.AddColumn(I18n.T("Mcp.Search.Column.Verified"));

                        foreach (var server in results)
                        {
                            var author = server.Author is null
                                ? $"[grey]{I18n.T("Common.NotApplicable")}[/]"
                                : server.Author.EscapeMarkup();
                            table.AddRow(
                                $"[blue]{server.QualifiedName.EscapeMarkup()}[/]",
                                server.Description.EscapeMarkup(),
                                author,
                                (server.DownloadCount?.ToString() ?? "0"),
                                server.Verified ? $"[green]{I18n.T("Common.Yes")}[/]" : $"[grey]{I18n.T("Common.No")}[/]");
                        }

                        AnsiConsole.Write(table);
                    });
                
                return 0;
            });
        }, queryArg);

        return command;
    }

    private static Command CreateShow(IHost host)
    {
        var command = new Command("show", I18n.T("Mcp.Show.Description"));
        var nameArg = new Argument<string>("name", I18n.T("Mcp.Show.NameArg"));
        command.AddArgument(nameArg);

        command.SetHandler(async name =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var smithery = host.Services.GetRequiredService<ISmitheryClient>();
                
                var server = await smithery.GetServerAsync(name);

                var panelContent = I18n.T(
                    "Mcp.Show.Summary",
                    server.Name.EscapeMarkup(),
                    server.QualifiedName.EscapeMarkup(),
                    Environment.NewLine,
                    (server.Author ?? I18n.T("Common.NotApplicable")).EscapeMarkup(),
                    (server.Homepage ?? I18n.T("Common.NotApplicable")).EscapeMarkup(),
                    server.DownloadCount?.ToString() ?? "0",
                    server.Verified ? $"[green]{I18n.T("Common.Yes")}[/]" : $"[grey]{I18n.T("Common.No")}[/]",
                    server.Description.EscapeMarkup());

                AnsiConsole.Write(new Panel(new Markup(panelContent))
                {
                    Header = new PanelHeader(I18n.T("Mcp.Show.Panel")),
                    Border = BoxBorder.Rounded
                });

                if (server.McpConfig != null)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine(I18n.T("Mcp.Show.InstallCommand"));
                    AnsiConsole.MarkupLine($"[grey]{server.McpConfig.Command.EscapeMarkup()} {string.Join(" ", server.McpConfig.Args).EscapeMarkup()}[/]");
                    
                    if (server.McpConfig.Env?.Count > 0)
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine(I18n.T("Mcp.Show.RequiredEnv"));
                        foreach (var env in server.McpConfig.Env)
                        {
                            var description = string.IsNullOrWhiteSpace(env.Value.Description)
                                ? $"[grey]{I18n.T("Mcp.Show.NoDescription")}[/]"
                                : env.Value.Description.EscapeMarkup();
                            var required = env.Value.Required
                                ? $"[red]({I18n.T("Common.Required")})[/]"
                                : $"[grey]({I18n.T("Common.Optional")})[/]";
                            AnsiConsole.MarkupLine($"  [yellow]{env.Key.EscapeMarkup()}[/]: {description} {required}");
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(server.Readme))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[bold]{I18n.T("Common.Readme")}[/]");
                    // Simple markdown rendering (we might want to use a more sophisticated renderer later)
                    AnsiConsole.WriteLine(server.Readme.Length > 1000 ? server.Readme[..1000] + "..." : server.Readme);
                }

                return 0;
            });
        }, nameArg);

        return command;
    }
}
