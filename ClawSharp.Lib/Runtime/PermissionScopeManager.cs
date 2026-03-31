namespace ClawSharp.Lib.Runtime;

using System.Threading;
using ClawSharp.Lib.Agents;

/// <summary>
/// Manages the current tool permission scope using AsyncLocal.
/// </summary>
public sealed class PermissionScopeManager : IPermissionScopeManager
{
    private static readonly AsyncLocal<PermissionScope?> _currentScope = new();

    /// <inheritdoc />
    public void SetScope(PermissionScope scope)
    {
        _currentScope.Value = scope;
    }

    /// <summary>
    /// Gets the current permission scope.
    /// </summary>
    public PermissionScope? GetCurrentScope() => _currentScope.Value;

    /// <inheritdoc />
    public bool CanInvokeTool(string toolName)
    {
        var scope = _currentScope.Value;
        
        // If no scope is explicitly set, we don't restrict by this manager.
        // The standard ToolPermissionSet will still be enforced by the ToolRegistry.
        if (scope == null || scope.AllowedToolNames.Count == 0)
        {
            return true;
        }

        return scope.AllowedToolNames.Contains(toolName);
    }
}
