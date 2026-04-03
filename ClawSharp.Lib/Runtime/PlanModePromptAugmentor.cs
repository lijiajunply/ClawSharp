namespace ClawSharp.Lib.Runtime;

internal static class PlanModePromptAugmentor
{
    public static string Build(SessionMode mode)
    {
        if (mode != SessionMode.Plan)
        {
            return string.Empty;
        }

        return
            "\n[PLAN MODE ACTIVE]\n" +
            "You are currently in **PLAN MODE**. Your goal is to research the codebase and design a detailed implementation plan.\n" +
            "1. **Restrictions**: You cannot modify any source files, run destructive shell commands, or perform any actions that change the system state. All such tools are disabled.\n" +
            "2. **Tools**: Use only read-only tools (`file_read`, `grep_search`, `list_files`, etc.) to gather information.\n" +
            "3. **Plan**: Create or update a `plan.md` file (or similar) describing your findings and proposed changes. Break the work down into small, verifiable tasks.\n" +
            "4. **Exit**: Once the plan is complete, use `exit_plan_mode` to submit it for user approval. Do not attempt to execute the plan while in Plan Mode.\n";
    }
}
