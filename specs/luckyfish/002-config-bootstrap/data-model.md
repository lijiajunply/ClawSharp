# Data Model: Configuration Bootstrap Wizard

## BootstrapConfig
Temporary structure used to gather user input during the wizard.

| Field | Type | Description |
|-------|------|-------------|
| WorkspaceRoot | string | Absolute or relative path to the workspace root. |
| DataPath | string | Subdirectory for runtime data. |
| DefaultProvider | string | ID of the selected AI provider (e.g., `openai`). |
| ProviderType | string | Implementation type (e.g., `openai-responses`). |
| ApiKey | string? | Masked secret key for the provider. |

## ProviderTemplate
Metadata for available providers shown in the selection list.

| Field | Type | Default Value |
|-------|------|---------------|
| Name | string | OpenAI / Anthropic / Gemini |
| Id | string | openai / anthropic / gemini |
| Type | string | openai-responses / anthropic-messages / etc. |
