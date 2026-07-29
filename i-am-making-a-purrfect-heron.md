# Minute Sheet App — Auth + Dashboard Build Plan

## Context

`minutesheet` is a fresh **.NET 8 Blazor Web App (Interactive Server)** with **ASP.NET Core Identity** already scaffolded against **SQL Server LocalDB** (`(localdb)\mssqllocaldb`). It currently ships the default template junk (Home "Hello world", Counter, Weather, generic nav) and a **no-op email sender**, so signup "works" but emails go nowhere.

The goal is a real internal tool: a branded **login/signup** flow with extended employee fields + real **email verification**, then a **dashboard** where employees create **minute sheets** (category, rich-text description, attachment) with a **dynamic approval workflow**, view **history**, and see a WIP **Actions** page. Brand color **`#000066`**, **Poppins** font, company logo, default template pages removed.

### Confirmed decisions
- **Email:** real SMTP now (via MailKit) — user supplies credentials in config/user-secrets.
- **Database:** keep LocalDB `(localdb)\mssqllocaldb`; connect the same in SSMS (Server name = `(localdb)\mssqllocaldb`).
- **Rich text:** self-hosted **Quill** editor.
- **Departments seeded:** HR, ICT, Finance, Admin (with per-department employee counts).

---

## 1. Data model & database

**Extend `Data/ApplicationUser.cs`** — add:
- `FullName` (string), `EmployeeNo` (string), `Designation` (enum), `DepartmentId` (int FK), `Department` nav.

**New enums** (`Data/Enums.cs`):
- `Designation { Executive, SeniorExecutive, AssistantManager, DeputyManager, SectionManager, UnitManager }` (with `[Display]` names for the dropdown).
- `SheetCategory { Financial, NonFinancial }`
- `ApprovalAction { Review, Approve }`

**New entities** (`Data/`):
- `Department { Id, Name, EmployeeCount }`
- `MinuteSheet { Id, Category, DescriptionHtml, AttachmentFileName, AttachmentStoredPath, CreatedByUserId, CreatedByUser, CreatedAt, Status }`
- `ApprovalStep { Id, MinuteSheetId, MinuteSheet, StepIndex, Action, ApproverEmail }`

**`Data/ApplicationDbContext.cs`** — add `DbSet<Department>`, `DbSet<MinuteSheet>`, `DbSet<ApprovalStep>`; in `OnModelCreating` seed the 4 departments (`HasData`) and configure relationships (cascade delete `MinuteSheet` → `ApprovalStep`).

**Migration:** add `dotnet ef migrations add AddMinuteSheetDomain` (new columns on `AspNetUsers` + new tables). Apply with `dotnet ef database update` (or the app's dev migrations endpoint). `Microsoft.EntityFrameworkCore.Tools` is already referenced.

---

## 2. Email verification — OTP code (real SMTP)

Verification is a **6-digit OTP emailed to the user**, entered on a verification page — not a click-through link.

- Add **MailKit** package to `minutesheet.csproj`.
- Add `Components/Account/SmtpEmailSender.cs`: a small service (registered in [Program.cs](minutesheet/Program.cs:36)) that sends plain mail via MailKit, reading an `EmailSettings` options class (`Host`, `Port`, `User`, `Password`, `From`, `EnableSsl`) bound from config. Expose a `SendOtpAsync(email, code)` method with a simple branded message ("Your verification code is **123456**"). It also satisfies `IEmailSender<ApplicationUser>` (replacing the no-op registration) so password-reset mail still works.
- Add an `EmailSettings` section to `appsettings.json` with **empty placeholder values**; real credentials go in **user-secrets** (`dotnet user-secrets set "EmailSettings:Password" ...`) — never commit secrets. Note in the plan output that the user must fill these (e.g. Gmail app password or SendGrid).
- **OTP generation/verification** uses Identity's built-in numeric email token provider (already available via `.AddDefaultTokenProviders()`):
  - Generate: `UserManager.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider, "email-verification")` → 6-digit code.
  - Verify: `UserManager.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, "email-verification", code)`; on success set `EmailConfirmed = true` and `UpdateAsync`.
- `RequireConfirmedAccount = true` stays on (already set), so unverified users can't log in.
- **Flow:** `Register.razor` creates the user (unconfirmed), generates the OTP, emails it, then redirects to a new **`/Account/VerifyOtp?email=...`** page. That page has a single code input + "Verify" (and a "Resend code" link). On valid code → mark confirmed → `SignInManager.SignInAsync(user)` → redirect to **`/`** (dashboard). This keeps the user in-session, honoring "after email is verified → dashboard".
- **New page:** `Components/Account/Pages/VerifyOtp.razor`. **Replaces** the old link flow: delete/retire `RegisterConfirmation.razor`; the existing `ConfirmEmail.razor` (link-based) is left unused by signup.

---

## 3. Signup / Login redesign

**`Register.razor`** — extend `InputModel` and form with: Name, Employee No., Designation dropdown (`InputSelect` over `Designation` enum), Department dropdown (`InputSelect` over `Department` list injected via `ApplicationDbContext`). Keep Email + Password (+ Confirm). In `RegisterUser`, after `CreateAsync` succeeds: set `FullName`/`EmployeeNo`/`Designation`/`DepartmentId` on the user, and **increment that department's `EmployeeCount`** (save in same unit of work). Drop the "Use another service" / external-login column.

**`Login.razor`** — drop the external-login column; keep email/password/remember-me; restyle.

**Layout & branding:**
- Restyle `Components/Account/Shared/AccountLayout.razor` (+ add a scoped CSS or use `app.css`) into a centered full-screen card: logo on top, brand `#000066` headings/buttons, Poppins.
- Serve the logo: copy `resources/images/FFC-Logo-Blue-V3.webp` → `minutesheet/wwwroot/images/FFC-Logo-Blue-V3.webp` (it currently lives outside `wwwroot`, so it isn't served). Reference as `images/FFC-Logo-Blue-V3.webp`.

---

## 4. Dashboard shell (remove template junk)

- **Delete** `Components/Pages/Counter.razor`, `Components/Pages/Weather.razor`, and the demo `Components/Pages/Auth.razor`.
- Rework `Components/Layout/NavMenu.razor` into the dashboard sidebar: logo + brand header, three nav links — **Create New Sheet** (`/dashboard/create`), **History** (`/dashboard/history`), **Actions** (`/dashboard/actions`) — plus the signed-in user's name + Logout (keep the existing `AuthorizeView` / logout form). Remove Home/Counter/Weather/Auth links.
- Rework `Components/Layout/MainLayout.razor` top-row (drop the "About" MS-Learn link) and restyle `MainLayout.razor.css` / `NavMenu.razor.css` with `#000066` + Poppins.
- Make **`/` (Home.razor)** the dashboard landing (add `@attribute [Authorize]`, welcome + quick links). Unauthenticated users already get redirected to Login via `RedirectToLogin` in [Routes.razor](minutesheet/Components/Routes.razor).

---

## 5. Minute Sheet form — `/dashboard/create`

New `Components/Pages/Dashboard/CreateSheet.razor` (`@rendermode InteractiveServer`, `[Authorize]`):
- **Category** — `InputSelect` (Financial / Non-Financial).
- **Description** — Quill editor. Add self-hosted Quill assets under `wwwroot/lib/quill/` (`quill.snow.css`, `quill.js`) and `wwwroot/js/quill-interop.js` (init editor, get HTML). Reference the CSS/JS from [App.razor](minutesheet/Components/App.razor). On submit, JS interop pulls the editor HTML into the model (`DescriptionHtml`).
- **Attachment** — `InputFile` limited to `.pdf,.doc,.docx` (validate extension **and** content); save to `wwwroot/uploads/` with a GUID filename, store original name + stored path.
- **Approval workflow table** — dynamic list of rows, each: `StepIndex` (auto number), `Action` dropdown (Review/Approve), `ApproverEmail` dropdown (populated from `UserManager.Users` emails). "Add row" / "remove row" buttons. **Enforce the last row = Approve** (force/lock the final row's action to Approve before save).
- **Submit** — persist `MinuteSheet` + its `ApprovalStep` rows via `ApplicationDbContext`, set `CreatedByUserId` from the current user, `CreatedAt = now`; redirect to History with a success message.

---

## 6. History — `/dashboard/history`

New `Components/Pages/Dashboard/History.razor` (`[Authorize]`): list the current user's minute sheets (table: date, category, status, attachment link, # approvals). Optional detail view/expand showing description + approval steps. Read-only.

## 7. Actions — `/dashboard/actions`

New `Components/Pages/Dashboard/Actions.razor` (`[Authorize]`): simple **"Work in progress (WIP)"** placeholder.

---

## 8. Global styling

- Add **Poppins** via a `<link>` to Google Fonts (or self-host under `wwwroot/lib/`) in [App.razor](minutesheet/Components/App.razor).
- In `wwwroot/app.css`: set `--brand: #000066`; set `html, body { font-family: 'Poppins', sans-serif; }`; override `.btn-primary`, link colors, and focus rings to the brand color (currently blue `#1b6ec2` / `#006bb7`).

---

## Files at a glance

- **New:** `Data/Enums.cs`, `Data/Department.cs`, `Data/MinuteSheet.cs`, `Data/ApprovalStep.cs`, `Components/Account/SmtpEmailSender.cs`, `Components/Account/Pages/VerifyOtp.razor`, `Components/Pages/Dashboard/CreateSheet.razor`, `History.razor`, `Actions.razor`, `wwwroot/js/quill-interop.js`, `wwwroot/lib/quill/*`, `wwwroot/images/FFC-Logo-Blue-V3.webp`, EF migration `AddMinuteSheetDomain`.
- **Modified:** `ApplicationUser.cs`, `ApplicationDbContext.cs`, `Program.cs`, `appsettings.json`, `Register.razor`, `Login.razor`, `AccountLayout.razor`, `NavMenu.razor(.css)`, `MainLayout.razor(.css)`, `Home.razor`, `App.razor`, `app.css`, `.csproj`.
- **Deleted:** `Counter.razor`, `Weather.razor`, `Auth.razor`, `RegisterConfirmation.razor`.

## Verification

1. `dotnet build` clean.
2. `dotnet ef database update` — confirm new tables/columns in **SSMS** (connect to `(localdb)\mssqllocaldb`); verify 4 seeded departments.
3. Run app (`dotnet run` / VS). Sign up with all fields → real email with a 6-digit OTP arrives (after SMTP creds set) → enter code on the VerifyOtp page → auto sign-in → dashboard. Confirm a wrong code is rejected and "Resend code" issues a new one.
4. Confirm the signer's department `EmployeeCount` incremented in SSMS.
5. Create a minute sheet: pick category, type rich text in Quill, upload a PDF and a .docx (reject a .txt), add/remove approval rows, confirm last row forced to Approve, submit → row saved (check DB) → appears in History.
6. Verify Actions shows WIP; confirm Counter/Weather/Home-template links are gone and branding (logo, `#000066`, Poppins) renders on auth + dashboard.
