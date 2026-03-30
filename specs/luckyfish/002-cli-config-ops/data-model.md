# Data Model: CLI Configuration Operations

## ConfigurationEntry
Represents a single configuration setting.

| Field | Type | Description |
|-------|------|-------------|
| Key | string | Configuration path (e.g., `Providers:DefaultProvider`) |
| Value | string? | Current value |
| Description | string? | Human-readable explanation |
| IsSecret | bool | Whether the value should be masked in UI |

## ConfigUpdate
Input for updating configuration.

| Field | Type | Description |
|-------|------|-------------|
| Key | string | Configuration path |
| Value | string? | New value |

## SecretMaskingPatterns
Default patterns for identifying secrets.
- `*.ApiKey`
- `*.Token`
- `*.Secret`
- `*.Key`
