namespace ClawSharp.Lib.Core;

/// <summary>
/// Specifies the source of a definition (Agent, Skill, etc.).
/// </summary>
public enum DynamicSourceType
{
    /// <summary>
    /// Definition is built-in to the library.
    /// </summary>
    BuiltIn,

    /// <summary>
    /// Definition is located in the project workspace.
    /// </summary>
    Workspace,

    /// <summary>
    /// Definition is located in the user's home directory.
    /// </summary>
    User
}
