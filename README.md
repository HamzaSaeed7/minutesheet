# Minute Sheet

An internal tool for creating **minute sheets** and routing them through a dynamic, multi-step **approval workflow**. Built as a .NET 8 **Blazor Web App** (Interactive Server) with ASP.NET Core Identity, it provides branded sign-up/login with real email OTP verification, a dashboard for authoring minute sheets (rich text + attachments), a configurable approver chain with a review/resolve loop, and per-user history.

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
  - **Rich-text description** via a self-hosted **Quill** editor.
  - **Attachment** upload (`.pdf`, `.doc`, `.docx`, up to 10 MB), stored under `wwwroot/uploads/`.
  - **Dynamic approval workflow** — add/remove rows, set each step to **Review** or **Approve**, pick an approver; the **final step is always Approve**. A numeric input sets the total number of rows.

- **Approval flow** (`/dashboard/sheet/{token}`)
  - Each approver gets an emailed link to review or approve.
  - **Review** requires a comment; **Approve** does not.
  - **Review → Resolve loop:** a reviewed step returns to the creator, who resolves it (sending it back to the approver) and can make a **limited edit** to the description/attachment while approvals are in progress.
  - A sheet is **Approved** only when every step is approved.

- **Creator controls**
  - **Edit** a sheet — full edit while all steps are pending; description/attachment-only edit once reviews are in progress (workflow locked).
  - **Delete** a sheet (with confirmation) from the sheet view or history.

- **History & Actions**
  - `/dashboard/history` — the user's own sheets, with status, approval progress, and edit/delete/open actions.
  - `/dashboard/actions` — items **pending your approval** and **reviews to resolve**.

- **AI Summary Generation**
  - **Document Transcribing:** Automatically extracts textual content from attached PDF (`PdfPig`) and Word (`DocumentFormat.OpenXml`) files.
  - **Smart Summaries:** Connects to the OpenRouter AI API to generate professional summaries based on the sheet's description and transcribed attachment content.

- **Email delivery** — prefers the **Brevo HTTP API** (HTTPS/443, rarely blocked); falls back to **SMTP** (MailKit) when only SMTP settings are provided. Mail failures never break signup or a workflow action.

---

## Tech stack

| Area        | Choice |
|-------------|--------|
| Framework   | .NET 8 · Blazor Web App (Interactive Server render mode) |
| Auth        | ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`) |
| Data        | Entity Framework Core + SQL Server LocalDB |
| Email       | Brevo HTTP API or SMTP via MailKit |
| Rich text   | Quill (self-hosted under `wwwroot/lib/quill`) |
| Summaries   | OpenRouter API + PdfPig/DocumentFormat.OpenXml for text extraction |
| 2FA QR      | QRCoder |
| UI          | Bootstrap + custom CSS (`wwwroot/app.css`), Poppins |

---

## Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (targets `net8.0`)
- SQL Server **LocalDB** (included with Visual Studio or via [SqlLocalDB](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb))
- Optional: [`dotnet-ef`](https://learn.microsoft.com/ef/core/cli/dotnet) for CLI migrations (`dotnet tool install --global dotnet-ef`)

### 1. Clone

```bash
git clone https://github.com/HamzaSaeed7/minutesheet.git
cd minutesheet
```

### 2. Configure the database

The default connection string points at LocalDB and is set in [`minutesheet/appsettings.json`](minutesheet/appsettings.json):

```
Server=(localdb)\mssqllocaldb;Database=aspnet-minutesheet-...;Trusted_Connection=True;MultipleActiveResultSets=true
```

Apply the EF Core migrations to create the schema (and seed departments):

```bash
dotnet ef database update --project minutesheet
```

> No `dotnet-ef`? The app also runs the migrations endpoint in Development, so you can apply pending migrations from the error page on first run.

### 3. Configure secrets (email and API keys)

`appsettings.json` ships with **empty** placeholders — **never commit real credentials**. Provide them via [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) instead. A `UserSecretsId` is already set on the project.

**OpenRouter AI Summarization (Required for Summaries):**

To enable AI summaries and transcription of attachments, provide an OpenRouter API key:

```bash
cd minutesheet
dotnet user-secrets set "OpenRouterSettings:ApiKey" "sk-or-v1-your-key"
```

**Email Option A — Brevo HTTP API (recommended):**

```bash
cd minutesheet
dotnet user-secrets set "EmailSettings:ApiKey" "xkeysib-your-key"
dotnet user-secrets set "EmailSettings:From" "verified-sender@yourdomain.com"
```

**Option B — SMTP (e.g. Gmail app password / SendGrid):**

```bash
cd minutesheet
dotnet user-secrets set "EmailSettings:Host" "smtp.example.com"
dotnet user-secrets set "EmailSettings:Port" "587"
dotnet user-secrets set "EmailSettings:User" "you@example.com"
dotnet user-secrets set "EmailSettings:Password" "your-app-password"
dotnet user-secrets set "EmailSettings:From" "you@example.com"
```

If neither is configured, the app runs but logs a warning and does **not** send mail (OTP won't arrive — sign-up still reaches the verify page, but you won't get a code).

### 4. Run

```bash
dotnet run --project minutesheet
```

Then open the URL shown in the console (defaults: `http://localhost:5285`, `https://localhost:7054`).

---

## Configuration reference

`EmailSettings` (bind from config or user-secrets):

| Key        | Description |
|------------|-------------|
| `ApiKey`   | Brevo HTTP API key (`xkeysib-…`). If set, the API transport is used. |
| `Host`     | SMTP host (fallback, used only when `ApiKey` is empty). |
| `Port`     | SMTP port (`587` StartTLS, `465` SSL-on-connect). |
| `User`     | SMTP username. |
| `Password` | SMTP password / app password. |
| `EnableSsl`| Use TLS for SMTP. |
| `From`     | Sender address (must be a verified sender). |
| `FromName` | Sender display name (default `Minute Sheet`). |

`OpenRouterSettings` (bind from config or user-secrets):

| Key        | Description |
|------------|-------------|
| `ApiKey`   | OpenRouter API key for generating AI document summaries (`sk-or-v1-...`). |

`ConnectionStrings:DefaultConnection` — the SQL Server connection string.

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
│  │  ├─ Routes.razor
│  │  ├─ Account/                   # Identity pages + SmtpEmailSender + OTP verify
│  │  ├─ Layout/                    # MainLayout, NavMenu (dashboard shell)
│  │  └─ Pages/
│  │     ├─ Home.razor              # dashboard landing
│  │     ├─ Settings.razor
│  │     └─ Dashboard/              # CreateSheet, History, SheetView, Actions
│  ├─ Data/
│  │  ├─ ApplicationDbContext.cs
│  │  ├─ ApplicationUser.cs         # extended Identity user
│  │  ├─ MinuteSheet.cs, ApprovalStep.cs, SheetComment.cs, Department.cs
│  │  ├─ Enums.cs, ApprovalWorkflow.cs
│  │  └─ Migrations/
│  └─ wwwroot/                      # app.css, bootstrap, quill, images, uploads/
```

---

## Data model

- **ApplicationUser** — Identity user + `FullName`, `EmployeeNo`, `Designation`, `DepartmentId`, `AvatarPath`.
- **Department** — `Name`, `EmployeeCount` (HR / ICT / Finance / Admin seeded).
- **MinuteSheet** — `Category`, `DescriptionHtml`, attachment name/path, `CreatedByUser`, `CreatedAt`, `Status`, unguessable `Token` (for shareable links).
- **ApprovalStep** — ordered step with `Action` (Review/Approve) and `Status` (Pending/Reviewed/Approved); cascades from its sheet.
- **SheetComment** — review / approval / resolution notes tied to a sheet and step.

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

- **Secrets never belong in the repo.** Email credentials go in user-secrets (dev) or environment/secret store (deploy).
- Uploaded attachments (`wwwroot/uploads/`) are runtime data and are git-ignored.
