# Minute Sheet Setup Guide for Hamza Bhai

This guide explains how to properly set up the local environment to get the Speech-to-Text and AI Translation features working on your machine after pulling the latest `nafees-updated-speech` branch.

## 1. Local Whisper Setup (Speech-to-Text)

We have integrated OpenAI's `whisper` CLI to run locally for transcribing audio (Urdu and English). 

**Requirements:**
1. You must have Python installed.
2. You must have `ffmpeg` installed (accessible in your system PATH).
3. Install the whisper CLI globally by running:
   ```powershell
   pip install -U openai-whisper setuptools-rust
   ```

**Model Configuration:**
By default, Whisper uses the heavy `small` model which can take a long time to transcribe on a standard CPU. We have updated the `appsettings.json` to use the `base` model instead. It is much faster and still provides decent accuracy for basic dictation. 

```json
"LocalWhisper": {
  "Model": "base"
}
```

## 2. OpenRouter AI Setup (Translation & Summarization)

We are using OpenRouter to power the AI features of the Minute Sheet app. Specifically, we have locked all 4 AI endpoints to use the `google/gemma-4-26b-a4b-it:free` model. This model is very capable and won't throw rate-limit or "content-safety" errors as frequently as the generic free pools.

### The 4 API Keys:
The system expects 4 separate API keys (at indexes 0, 1, 2, and 3) for the following services:
1. `ApiKeys:0` -> Used for generating the Minute Sheet **Summary**.
2. `ApiKeys:1` -> Used for extracting **Action Items**.
3. `ApiKeys:2` -> Used for extracting the **Agenda**.
4. `ApiKeys:3` -> Used for **Translating** Urdu transcription to English.

*(You can use the exact same OpenRouter API key for all 4 if you'd like, but they must be configured at all 4 indexes).*

### How to configure the keys securely:
Do **not** put the API keys directly into `appsettings.json`, as they will get pushed to GitHub. Instead, you must use the .NET User Secrets manager.

Open your terminal in the `minutesheet` folder (where the `.csproj` file is) and run these commands to set up the keys locally on your laptop:

```powershell
# Set up Key 1 (Summary)
dotnet user-secrets set "OpenRouterSettings:ApiKeys:0" "your-openrouter-api-key-here"

# Set up Key 2 (Action Items)
dotnet user-secrets set "OpenRouterSettings:ApiKeys:1" "your-openrouter-api-key-here"

# Set up Key 3 (Agenda)
dotnet user-secrets set "OpenRouterSettings:ApiKeys:2" "your-openrouter-api-key-here"

# Set up Key 4 (Urdu-to-English Translation)
dotnet user-secrets set "OpenRouterSettings:ApiKeys:3" "your-openrouter-api-key-here"
```

To verify that your keys are placed correctly, you can run:
```powershell
dotnet user-secrets list
```

## 3. Running the App
Once you have Whisper installed and the User Secrets configured, simply run the backend:
```powershell
dotnet run
```
And you are good to go!
