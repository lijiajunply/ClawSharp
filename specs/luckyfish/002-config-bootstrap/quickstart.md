# Quickstart: Configuration Bootstrap Wizard

## Triggering the Wizard

The wizard triggers automatically when you run ClawSharp for the first time in a new directory.

```bash
# Run any command
claw chat
```

### Scenario 1: Fresh Setup
If no configuration exists:
1. You will see a welcome banner.
2. Press Enter to accept `.` as your workspace.
3. Press Enter to accept `.clawsharp` as your data path.
4. Use arrow keys to select `OpenAI`.
5. Paste your API key (it will be hidden).
6. The app creates `appsettings.json` and immediately starts the chat session.

### Scenario 2: Existing Local Config
If `appsettings.Local.json` exists but `appsettings.json` is missing:
1. You will be asked if you want to use the existing local config.
2. If you choose "Yes", the wizard is skipped.
3. If you choose "No", the wizard proceeds to create a new `appsettings.json`.

## Manual Reset
To re-run the setup, simply delete `appsettings.json`:
```bash
rm appsettings.json
claw list
```
