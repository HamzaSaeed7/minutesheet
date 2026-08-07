# Minute Sheet Project & Creation Tab Documentation

## 1. Project Brief Description

**Minute Sheet** is an internal tool for creating minute sheets and routing them through a dynamic, multi-step approval workflow. It is built as a .NET 8 Blazor Web App (Interactive Server) with ASP.NET Core Identity.

### Key Features:
- **Authentication:** Branded sign-up/login with email OTP verification.
- **Minute Sheets:** Dashboard for authoring minute sheets (rich text + attachments), configurable approver chain with a review/resolve loop.
- **Workflow:** Dynamic approval workflow where each step can be set to "Review" or "Approve". The final step is always "Approve".
- **AI Summary Generation:** Automatically extracts textual content from attached PDF/Word files and generates summaries using the OpenRouter AI API.
- **Email Delivery:** Utilizes Brevo HTTP API or SMTP via MailKit for email notifications.

---

## 2. Create Minute Sheet Tab Information

The "Create Minute Sheet" tab (located at `/dashboard/create`) is the primary interface for users to author new minute sheets.

### Features of the Create Tab:
- **Category Selection:** Users can mark the sheet as **Financial** or **Non-Financial**.
- **Confidentiality:** Toggle between **Non-Confidential** (viewable by all users) and **Confidential** (viewable only by the creator, approvers, and admins).
- **Rich-Text Description:** An integrated **Quill** editor for rich-text input. It also includes a voice dictation feature (English and Urdu).
- **Attachment Upload:** Support for `.pdf`, `.doc`, and `.docx` files (up to 10 MB).
- **Dynamic Approval Workflow Configuration:**
  - Users can dynamically add or remove approver rows.
  - Each step can be assigned to a specific user and designated as either a **Review** or **Approve** step.
  - The final step in the chain is strictly enforced as an **Approve** step.

---

## 3. Code and Files Responsible

The primary file responsible for the "Create Minute Sheet" UI and frontend logic is the Blazor component `CreateSheet.razor`.

### Key Files:
- **UI Component:** `minutesheet/Components/Pages/Dashboard/CreateSheet.razor`
- **Data Models:** 
  - `minutesheet/Data/MinuteSheet.cs`
  - `minutesheet/Data/ApprovalStep.cs`
- **Database Context:** `minutesheet/Data/ApplicationDbContext.cs`

### `CreateSheet.razor` Overview:

Here is a brief look at the header and structure of the `CreateSheet.razor` file, showing the injected services and the beginning of the UI layout:

```csharp
@page "/dashboard/create"
@page "/dashboard/sheet/{Token:guid}/edit"
@rendermode InteractiveServer
@implements IAsyncDisposable

@using System.ComponentModel.DataAnnotations
@using Microsoft.AspNetCore.Authorization
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Identity
@using Microsoft.EntityFrameworkCore
@using minutesheet.Data
@using minutesheet.Components.Account
@using minutesheet.Services

@attribute [Authorize]

@inject ApplicationDbContext DbContext
@inject UserManager<ApplicationUser> UserManager
@inject IJSRuntime JS
@inject IWebHostEnvironment Env
@inject NavigationManager NavigationManager
@inject EmailQueue EmailQueue
@inject minutesheet.Services.DocumentSummarizationService SummarizationService
@inject minutesheet.Services.ToastService ToastService

<PageTitle>@(_isEdit ? "Edit Minute Sheet" : "Create Minute Sheet")</PageTitle>

<ToastHost />

<!-- Form UI for Category, Confidentiality, Description (Quill Editor), and Attachments -->
<div class="create-grid">
    <div class="create-col">
        <!-- Category Selection -->
        <div class="content-card">
            <div class="card-eyebrow">Category</div>
            <div class="seg-tabs">
                <button type="button" @onclick="() => _category = SheetCategory.Financial">Financial</button>
                <button type="button" @onclick="() => _category = SheetCategory.NonFinancial">Non-Financial</button>
            </div>
        </div>

        <!-- Confidentiality Toggle -->
        <!-- Description Editor with Dictation -->
        <!-- File Upload Dropzone -->
    </div>
    
    <!-- Dynamic Approval Workflow Setup Column -->
</div>
```

The component handles both **Creating** new sheets and **Editing** existing ones (if the workflow hasn't progressed past the initial stages). It uses Entity Framework Core to save the `MinuteSheet` entity along with its associated `ApprovalStep` entities to the SQL Server database.
