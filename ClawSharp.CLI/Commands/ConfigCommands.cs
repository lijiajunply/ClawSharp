using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class ConfigCommands
{
    public static Command Create(IHost host)
    {
        var configCommand = new Command("config", "Manage ClawSharp configuration settings");

        configCommand.AddCommand(CreateList(host));
        configCommand.AddCommand(CreateGet(host));
        configCommand.AddCommand(CreateSet(host));
        configCommand.AddCommand(CreateReset(host));

        return configCommand;
    }

    private static Command CreateList(IHost host)
    {
        var command = new Command("list", "Show all configuration settings");
        var allOption = new Option<bool>("--all", "Show all supported keys, even if not explicitly set");
        command.AddOption(allOption);

        command.SetHandler(async (showAll) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var configManager = host.Services.GetRequiredService<IConfigManager>();
                var allConfigs = await configManager.GetAllAsync();
                
                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn("Key");
                table.AddColumn("Value");

                var keysToShow = showAll 
                    ? await configManager.GetSupportedKeysAsync() 
                    : allConfigs.Keys.OrderBy(k => k);

                foreach (var key in keysToShow.OrderBy(k => k))
                {
                    // 过滤掉 IConfiguration 自动生成的一些内部键（如 : 分隔符产生的空键）
                    if (string.IsNullOrWhiteSpace(key)) continue;

                    string? displayValue;
                    if (allConfigs.TryGetValue(key, out var val))
                    {
                        displayValue = configManager.IsSecret(key) ? "********" : val;
                    }
                    else
                    {
                        displayValue = "[grey]<not set>[/]";
                    }

                    table.AddRow(key.EscapeMarkup(), displayValue?.EscapeMarkup() ?? "[grey]<null>[/]");
                }

                AnsiConsole.Write(table);
                return 0;
            });
        }, allOption);

        return command;
    }

    private static Command CreateGet(IHost host)
    {
        var command = new Command("get", "Retrieve the value of a specific setting");
        var keyArg = new Argument<string>("key", "The configuration key to retrieve");
        command.AddArgument(keyArg);

        command.SetHandler(async (key) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var configManager = host.Services.GetRequiredService<IConfigManager>();
                var value = configManager.Get(key);
                
                if (value == null)
                {
                    AnsiConsole.MarkupLine($"[yellow]Key not found or not set: {key.EscapeMarkup()}[/]");
                }
                else
                {
                    var displayValue = configManager.IsSecret(key) ? "********" : value;
                    AnsiConsole.WriteLine(displayValue);
                }
                return 0;
            });
        }, keyArg);

        return command;
    }

    private static Command CreateSet(IHost host)
    {
        var command = new Command("set", "Set a configuration value");
        var keyArg = new Argument<string>("key", "The configuration key to set");
        var valueArg = new Argument<string?>("value", () => null, "The value to set (optional for secrets, will trigger prompt)");
        command.AddArgument(keyArg);
        command.AddArgument(valueArg);

        command.SetHandler(async (key, value) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var configManager = host.Services.GetRequiredService<IConfigManager>();
                
                var finalValue = value;
                if (string.IsNullOrEmpty(finalValue) && configManager.IsSecret(key))
                {
                    finalValue = AnsiConsole.Prompt(
                        new TextPrompt<string>($"Enter value for [blue]{key.EscapeMarkup()}[/]:")
                            .Secret());
                }

                if (finalValue == null)
                {
                     AnsiConsole.MarkupLine("[red]Value is required.[/]");
                     return 1;
                }

                await configManager.SetAsync(key, finalValue);
                AnsiConsole.MarkupLine($"[green]Successfully set {key.EscapeMarkup()}[/]");
                return 0;
            });
        }, keyArg, valueArg);

        return command;
    }

    private static Command CreateReset(IHost host)
    {
        var command = new Command("reset", "Reset configuration to defaults");
        var allOption = new Option<bool>("--all", "Reset all local settings");
        var keyOption = new Option<string?>("--key", "Reset a specific key");
        var forceOption = new Option<bool>("--force", "Skip confirmation prompt");
        
        command.AddOption(allOption);
        command.AddOption(keyOption);
        command.AddOption(forceOption);

        command.SetHandler(async (all, key, force) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                if (!all && string.IsNullOrEmpty(key))
                {
                    AnsiConsole.MarkupLine("[red]Either --all or --key must be specified.[/]");
                    return 1;
                }

                if (!force)
                {
                    var message = all ? "Are you sure you want to reset ALL settings?" : $"Are you sure you want to reset [blue]{key.EscapeMarkup()}[/]?";
                    if (!AnsiConsole.Confirm(message))
                    {
                        AnsiConsole.MarkupLine("[grey]Operation cancelled.[/]");
                        return 0;
                    }
                }

                var configManager = host.Services.GetRequiredService<IConfigManager>();
                await configManager.ResetAsync(all, key, force);
                AnsiConsole.MarkupLine("[green]Reset completed successfully.[/]");
                return 0;
            });
        }, allOption, keyOption, forceOption);

        return command;
    }
}
