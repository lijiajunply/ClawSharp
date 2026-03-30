# Quickstart: CLI Configuration Operations

## Configuration Setup

To use the new configuration operations, ensure you have the latest `ClawSharp.CLI` installed.

### Listing All Settings
```bash
claw config list
```
Displays a table of all keys and their masked values.

### Setting an API Key
```bash
# Set directly via argument
claw config set Providers:Models:0:ApiKey sk-your-key-here

# Or set via interactive prompt (safer for secrets)
claw config set Providers:Models:0:ApiKey
# Output: Enter value for Providers:Models:0:ApiKey: [input masked]
```

### Retrieving a Setting
```bash
claw config get Providers:DefaultProvider
# Output: openai
```

### Resetting Configuration
```bash
# Reset a specific key
claw config reset --key Providers:DefaultProvider

# Reset everything to factory defaults
claw config reset --all
```

## Security Note
Sensitive keys (ending in `.ApiKey`, `.Token`, `.Secret`, `.Key`) are automatically masked in the `list` output. Use the interactive `set` command to avoid leaking keys in your shell history.
