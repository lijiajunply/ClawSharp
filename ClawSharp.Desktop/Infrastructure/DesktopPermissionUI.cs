using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ClawSharp.Lib.Runtime;
using ClawSharp.Lib.Tools;
using ClawSharp.Desktop.Views;

namespace ClawSharp.Desktop.Infrastructure;

public class DesktopPermissionUI : IPermissionUI
{
    public async Task<bool> RequestCapabilityAsync(string agentId, ToolCapability capability, string toolName, CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new PermissionDialogView(
                $"Permission Required",
                $"Agent '{agentId}' is requesting '{capability}' capability for tool '{toolName}'.\n\nAllow this capability?"
            );

            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow != null)
                {
                    return await dialog.ShowDialog<bool>(mainWindow);
                }
            }
            
            // Fallback
            dialog.Show();
            return false; // Safest default if no main window exists
        });
    }

    public async Task<bool> RequestApprovalAsync(string agentId, string toolName, JsonElement payload, CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            string details = "";
            if (toolName == "shell_run")
            {
                var command = payload.TryGetProperty("command", out var cmd) ? cmd.GetString() : "Unknown command";
                details = $"Command to run:\n{command}";
            }
            else if (toolName == "file_write")
            {
                var path = payload.TryGetProperty("path", out var p) ? p.GetString() : "Unknown path";
                var content = payload.TryGetProperty("content", out var c) ? c.GetString() : "Unknown content";
                details = $"File: {path}\nContent snippet:\n{content?[..System.Math.Min(content.Length, 200)]}...";
            }
            else
            {
                details = payload.GetRawText();
            }

            var dialog = new PermissionDialogView(
                $"Approval Required: {toolName}",
                $"Agent '{agentId}' wants to execute '{toolName}'.\n\nDetails:\n{details}\n\nDo you want to allow this execution?"
            );

            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow != null)
                {
                    return await dialog.ShowDialog<bool>(mainWindow);
                }
            }

            return false;
        });
    }
}
