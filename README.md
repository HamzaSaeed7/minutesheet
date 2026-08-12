# Minute Sheet

An internal tool for creating **minute sheets** and routing them through a dynamic, multi-step **approval workflow**. Built as a .NET 8 **Blazor Web App** (Interactive Server) with ASP.NET Core Identity, it provides branded sign-up/login with real email OTP verification, a dashboard for authoring minute sheets (rich text + attachments + audio dictation), a configurable approver chain with a review/resolve loop, and per-user history.

Brand: `#000066` · Poppins.

---

## Features

- **Authentication & accounts**
  - Branded sign-up / login (email + password), with extended employee fields: full name, employee number, designation, and department.
  - **Email verification via 6-digit OTP** (Identity's numeric token provider) — unverified users can't sign in.
  - Password reset, 2FA, and account management (from the ASP.NET Core Identity scaffold).
  - Departments seeded on first run: **HR, ICT, Finance, Admin**.

- **Minute sheets** (`/dashboard/create`)
  - Category: **Financial / Non-Financial**.
  - **Confidentiality:** Toggle between **Non-Confidential** (viewable by all) and **Confidential** (viewable only by creator, approvers, and admins).
  - **Rich-text description** via a self-hosted **Quill** editor.
  - **Audio Dictation:** Real-time speech-to-text using local Whisper CLI integration (supports English and Urdu), with Urdu-to-English translation capabilities.
  - **Attachment** upload (`.pdf`, `.doc`, `.docx`, up to 10 MB), stored under `wwwroot/uploads/`.
  - **Dynamic approval workflow** — add/remove rows, set each step to **Review** or **Approve**, pick an approver; the **final step is always Approve**. 

- **Approval flow** (`/dashboard/sheet/{token}`)
  - Each approver gets an emailed link to review or approve.
  - **Review** requires a comment; **Approve** does not.
  - **Review → Resolve loop:** a reviewed step returns to the creator, who resolves it and can make a **limited edit** to the description/attachment while approvals are in progress.
  - A sheet is **Approved** only when every step is approved.

- **History & Actions**
  - `/dashboard/history` — the user's own sheets, with status, approval progress, and edit/delete/open actions.
  - `/dashboard/actions` — items **pending your approval** and **reviews to resolve**.

- **AI Document Summarization & Extraction**
  - **Document Transcribing:** Automatically extracts textual content from attached PDF (`PdfPig`) and Word (`DocumentFormat.OpenXml`) files.
  - **Smart Summaries & Extraction:** Connects to the OpenRouter AI API (`google/gemma-4-26b-a4b-it:free`) to generate professional **Summaries**, **Action Items**, and **Agendas** based on the sheet's description and transcribed attachment content.
  - **Translation:** Translates Urdu transcriptions to English.

- **Email delivery** — prefers the **Brevo HTTP API** (HTTPS/443, rarely blocked); falls back to **SMTP** (MailKit) when only SMTP settings are provided. 

---

## Tech stack

| Area        | Choice |
|-------------|--------|
| Framework   | .NET 8 · Blazor Web App (Interactive Server render mode) |
| Auth        | ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`) |
| Data        | Entity Framework Core + SQL Server LocalDB |
| Email       | Brevo HTTP API or SMTP via MailKit |
| Rich text   | Quill (self-hosted under `wwwroot/lib/quill`) |
| AI / LLM    | OpenRouter API (`google/gemma-4-26b-a4b-it:free`) |
| Dictation   | Local Whisper CLI via Python + `ffmpeg` |
| Text Extract| PdfPig & DocumentFormat.OpenXml for text extraction |
| 2FA QR      | QRCoder |
| UI          | Bootstrap + custom CSS (`wwwroot/app.css`), Poppins |

---

## Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (targets `net8.0`)
- SQL Server **LocalDB** (included with Visual Studio or via SqlLocalDB)
- **Python** and **ffmpeg** (for Audio Dictation)
- Whisper CLI: `pip install -U openai-whisper setuptools-rust`
- Optional: `dotnet-ef` for CLI migrations (`dotnet tool install --global dotnet-ef`)

### 1. Clone

```bash
git clone https://github.com/HamzaSaeed7/minutesheet.git
cd minutesheet
```

### 2. Configure the database

The default connection string points at LocalDB in `minutesheet/appsettings.json`:

```
Server=(localdb)\mssqllocaldb;Database=aspnet-minutesheet-...;Trusted_Connection=True;MultipleActiveResultSets=true
```

Apply the EF Core migrations to create the schema (and seed departments):

```bash
dotnet ef database update --project minutesheet
```

### 3. Configure secrets (email and API keys)

`appsettings.json` ships with **empty** placeholders — **never commit real credentials**. Provide them via user-secrets instead.

**OpenRouter AI Settings (Required for AI Summaries, Actions, Agendas, Translation):**
The system expects 4 separate API keys for different tasks (you can use the same key for all 4).
```bash
cd minutesheet
dotnet user-secrets set "OpenRouterSettings:ApiKeys:0" "your-openrouter-key-for-summary"
dotnet user-secrets set "OpenRouterSettings:ApiKeys:1" "your-openrouter-key-for-action-items"
dotnet user-secrets set "OpenRouterSettings:ApiKeys:2" "your-openrouter-key-for-agenda"
dotnet user-secrets set "OpenRouterSettings:ApiKeys:3" "your-openrouter-key-for-translation"
```

**Local Whisper Model:**
In `appsettings.json`, set the whisper model (defaults to `base`):
```json
"LocalWhisper": {
  "Model": "base"
}
```

**Email Option A — Brevo HTTP API (recommended):**
```bash
dotnet user-secrets set "EmailSettings:ApiKey" "xkeysib-your-key"
dotnet user-secrets set "EmailSettings:From" "verified-sender@yourdomain.com"
```

**Email Option B — SMTP (e.g. Gmail app password / SendGrid):**
```bash
dotnet user-secrets set "EmailSettings:Host" "smtp.example.com"
dotnet user-secrets set "EmailSettings:Port" "587"
dotnet user-secrets set "EmailSettings:User" "you@example.com"
dotnet user-secrets set "EmailSettings:Password" "your-app-password"
dotnet user-secrets set "EmailSettings:From" "you@example.com"
```

### 4. Run

```bash
dotnet run --project minutesheet
```

Then open the URL shown in the console (defaults: `http://localhost:5285`, `https://localhost:7054`).

### 5. Seed data

On startup in Development the app restores a snapshot of a working database from
`minutesheet/Data/Seed/seed-data.json` — departments, users, minute sheets and their
full approval/comment history — so a fresh clone comes up with realistic data instead
of an empty dashboard.

- Every seeded account signs in with the password `Abcd1234!`. The export carries **no
  password hashes**, so real credentials never reach the repo.
- Seeding is idempotent: rows already present (matched on their original primary key)
  are skipped, so restarting never duplicates anything.
- It is off outside Development. Override either way with:

```bash
dotnet user-secrets set "Seed:LoadSnapshot" "false"
```

To refresh the snapshot from your own database, re-export each table with
`FOR JSON PATH` into the same file shape (`departments`, `users`, `userRoles`, `sheets`,
`approvalSteps`, `comments`, `shares`, `suggestions`, `vocabulary`).

---

## Configuration reference

`EmailSettings`:
- `ApiKey`: Brevo HTTP API key (`xkeysib-…`).
- `Host`, `Port`, `User`, `Password`, `EnableSsl`, `From`, `FromName`: Standard SMTP settings.

`OpenRouterSettings`:
- `ApiKeys`: Array of OpenRouter API keys (`[0]=Summary`, `[1]=Actions`, `[2]=Agenda`, `[3]=Translation`).

`LocalWhisper`:
- `Model`: Whisper model to use (e.g., `base`, `small`).

`ConnectionStrings:DefaultConnection`: SQL Server connection string.

`Seed:LoadSnapshot`: Restore `Data/Seed/seed-data.json` at startup. Defaults to on in
Development, off elsewhere.

---

## Project structure

```
minutesheet/
├─ minutesheet.slnx                 # solution
├─ minutesheet/
│  ├─ Program.cs                    # DI, auth, EF, email transport wiring
│  ├─ appsettings.json              # config (empty email placeholders)
│  ├─ Components/
│  │  ├─ App.razor                  # root document, CSS cache-busting
│  │  ├─ Account/                   # Identity pages + SmtpEmailSender + OTP verify
│  │  ├─ Pages/
│  │     ├─ Dashboard/              # CreateSheet, History, SheetView, Actions
│  │     └─ Admin/                  # AllSheets, Departments, Users
│  ├─ Data/                         # EF Models (ApplicationUser, MinuteSheet, ApprovalStep, etc.)
│  ├─ Services/                     # DocumentSummarization, SpeechTranscription, SheetPdfService, etc.
│  └─ wwwroot/                      # app.css, bootstrap, quill, images, uploads/
```

---

## Data model

- **ApplicationUser** — Identity user + `FullName`, `EmployeeNo`, `Designation`, `DepartmentId`, `AvatarPath`.
- **Department** — `Name`, `EmployeeCount` (HR / ICT / Finance / Admin seeded).
- **MinuteSheet** — `Category`, `DescriptionHtml`, attachment name/path, `CreatedByUser`, `CreatedAt`, `Status`, unguessable `Token`.
- **ApprovalStep** — ordered step with `Action` (Review/Approve) and `Status` (Pending/Reviewed/Approved).
- **SheetComment** — review / approval / resolution notes.

---

## Branches

| Branch     | Purpose |
|------------|---------|
| `main`     | Integration branch. |
| `Frontend` | UI / Blazor component work. |
| `Backend`  | Server logic, workflow, email, identity. |
| `Database` | EF Core model & migrations. |

---

## Notes

- **Secrets never belong in the repo.** Email and OpenRouter credentials go in user-secrets.
- Uploaded attachments (`wwwroot/uploads/`) are runtime data and are git-ignored.
