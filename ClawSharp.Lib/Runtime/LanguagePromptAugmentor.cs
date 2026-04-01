namespace ClawSharp.Lib.Runtime;

internal static class LanguagePromptAugmentor
{
    public static string Build(string? sessionOutputLanguage, string? defaultOutputLanguage)
    {
        var effectiveLanguage = Normalize(sessionOutputLanguage) ?? Normalize(defaultOutputLanguage);
        if (effectiveLanguage is null)
        {
            return string.Empty;
        }

        return
            "[Output Language]\n" +
            $"Use {effectiveLanguage} as the default language for all user-facing responses.\n" +
            "- Keep code, file paths, commands, identifiers, and API names unchanged unless the user explicitly asks for translation.\n" +
            "- Do not switch to another language unless the user explicitly asks you to do so.\n" +
            "- If the user explicitly requests a different language for the current reply or task, follow the user's request.";
    }

    private static string? Normalize(string? outputLanguage)
    {
        if (string.IsNullOrWhiteSpace(outputLanguage))
        {
            return null;
        }

        return outputLanguage.Trim();
    }
}
