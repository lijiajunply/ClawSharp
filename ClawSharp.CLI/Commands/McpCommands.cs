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
        var command = new Command("mcp", "Manage Model Context Protocol (MCP) servers");
        command.AddCommand(CreateList(host));
        command.AddCommand(CreateSearch(host));
        command.AddCommand(CreateShow(host));
        command.AddCommand(CreateInstall(host));
        return command;
    }

    private static Command CreateInstall(IHost host)
    {
        var command = new Command("install", "Install an MCP server from the Smithery marketplace");
        var nameArg = new Argument<string>("name", "The qualified name of the server (e.g. owner/name)");
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
                    AnsiConsole.MarkupLine("[red]Error: This server does not provide an automated MCP configuration.[/]");
                    return 0;
                }

                AnsiConsole.MarkupLine($"[blue]Installing {server.QualifiedName.EscapeMarkup()}...[/]");

                var envVars = new Dictionary<string, string>();
                if (server.McpConfig.Env?.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[bold]Configuration Required:[/]");
                    foreach (var env in server.McpConfig.Env)
                    {
                        var prompt = $"Enter value for [yellow]{env.Key.EscapeMarkup()}[/]";
                        if (!string.IsNullOrWhiteSpace(env.Value.Description))
                        {
                            prompt += $" ({env.Value.Description.EscapeMarkup()})";
                        }
                        
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
                
                AnsiConsole.MarkupLine($"[green]Successfully installed {server.Name.EscapeMarkup()}![/]");
                AnsiConsole.MarkupLine("The server has been added to [grey]~/.clawsharp/mcp.json[/].");
                AnsiConsole.MarkupLine("Restart your session or use [blue]RELOAD[/] to apply changes.");

                return 0;
            });
        }, nameArg);

        return command;
    }

    private static Command CreateList(IHost host)
    {
        var command = new Command("list", "List locally configured MCP servers");
        command.SetHandler(async () =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var catalog = host.Services.GetRequiredService<IMcpServerCatalog>();
                var servers = catalog.GetAll();

                if (servers.Count == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]No MCP servers configured.[/]");
                    return 0;
                }

                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn("Name");
                table.AddColumn("Command");
                table.AddColumn("Arguments");
                table.AddColumn("Capabilities");

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
        var command = new Command("search", "Search MCP servers in the Smithery marketplace");
        var queryArg = new Argument<string?>("query", () => null, "Search query");
        command.AddArgument(queryArg);

        command.SetHandler(async query =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var smithery = host.Services.GetRequiredService<ISmitheryClient>();
                
                await AnsiConsole.Status()
                    .StartAsync("Searching Smithery...", async ctx =>
                    {
                        var results = await smithery.SearchServersAsync(query);

                        if (results.Count == 0)
                        {
                            AnsiConsole.MarkupLine("[yellow]No servers found in Smithery.[/]");
                            return;
                        }

                        var table = new Table().Border(TableBorder.Rounded).Expand();
                        table.AddColumn("Qualified Name");
                        table.AddColumn("Description");
                        table.AddColumn("Author");
                        table.AddColumn("Downloads");
                        table.AddColumn("Verified");

                        foreach (var server in results)
                        {
                            table.AddRow(
                                $"[blue]{server.QualifiedName.EscapeMarkup()}[/]",
                                server.Description.EscapeMarkup(),
                                (server.Author ?? "[grey]n/a[/]").EscapeMarkup(),
                                (server.DownloadCount?.ToString() ?? "[grey]0[/]"),
                                server.Verified ? "[green]Yes[/]" : "[grey]No[/]");
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
        var command = new Command("show", "Show detailed information for a Smithery MCP server");
        var nameArg = new Argument<string>("name", "The qualified name of the server (e.g. owner/name)");
        command.AddArgument(nameArg);

        command.SetHandler(async name =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var smithery = host.Services.GetRequiredService<ISmitheryClient>();
                
                var server = await smithery.GetServerAsync(name);

                var panelContent =
                    $"[bold]{server.Name.EscapeMarkup()}[/] ([blue]{server.QualifiedName.EscapeMarkup()}[/]){Environment.NewLine}" +
                    $"Author: {server.Author.EscapeMarkup() ?? "[grey]n/a[/]"}{Environment.NewLine}" +
                    $"Homepage: [link]{server.Homepage.EscapeMarkup() ?? "[grey]n/a[/]"}[/]{Environment.NewLine}" +
                    $"Downloads: {server.DownloadCount}{Environment.NewLine}" +
                    $"Verified: {(server.Verified ? "[green]Yes[/]" : "[grey]No[/]")}{Environment.NewLine}{Environment.NewLine}" +
                    $"{server.Description.EscapeMarkup()}";

                AnsiConsole.Write(new Panel(new Markup(panelContent))
                {
                    Header = new PanelHeader("Smithery MCP Server"),
                    Border = BoxBorder.Rounded
                });

                if (server.McpConfig != null)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[bold]Installation Command:[/]");
                    AnsiConsole.MarkupLine($"[grey]{server.McpConfig.Command.EscapeMarkup()} {string.Join(" ", server.McpConfig.Args).EscapeMarkup()}[/]");
                    
                    if (server.McpConfig.Env?.Count > 0)
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine("[bold]Required Environment Variables:[/]");
                        foreach (var env in server.McpConfig.Env)
                        {
                            AnsiConsole.MarkupLine($"  [yellow]{env.Key.EscapeMarkup()}[/]: {env.Value.Description.EscapeMarkup() ?? "[grey]No description[/]"} {(env.Value.Required ? "[red](Required)[/]" : "[grey](Optional)[/]")}");
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(server.Readme))
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[bold]README[/]");
                    // Simple markdown rendering (we might want to use a more sophisticated renderer later)
                    AnsiConsole.WriteLine(server.Readme.Length > 1000 ? server.Readme[..1000] + "..." : server.Readme);
                }

                return 0;
            });
        }, nameArg);

        return command;
    }
}
