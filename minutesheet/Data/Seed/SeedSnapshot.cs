using System.Text.Json;
using System.Text.Json.Serialization;

namespace minutesheet.Data.Seed
{
    // Shape of Data/Seed/seed-data.json — a snapshot of a working database,
    // exported table by table. Ids are preserved so the relationships between
    // sheets, steps, comments, shares and suggestions survive the round trip.
    public sealed class SeedSnapshot
    {
        public List<SeedDepartment> Departments { get; set; } = new();
        public List<SeedUser> Users { get; set; } = new();
        public List<SeedUserRole> UserRoles { get; set; } = new();
        public List<SeedSheet> Sheets { get; set; } = new();
        public List<SeedApprovalStep> ApprovalSteps { get; set; } = new();
        public List<SeedComment> Comments { get; set; } = new();
        public List<SeedShare> Shares { get; set; } = new();
        public List<SeedSuggestion> Suggestions { get; set; } = new();
        public List<SeedVocabularyTerm> Vocabulary { get; set; } = new();

        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        // Reads the snapshot next to the app binaries. Returns null when the file
        // is absent, so a deployment that ships without it simply skips seeding.
        public static async Task<SeedSnapshot?> LoadAsync(string path, CancellationToken ct = default)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<SeedSnapshot>(stream, Options, ct);
        }
    }

    public sealed class SeedDepartment
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int EmployeeCount { get; set; }
    }

    public sealed class SeedUser
    {
        public string Id { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public bool EmailConfirmed { get; set; }
        public string FullName { get; set; } = "";
        public string EmployeeNo { get; set; } = "";
        public int Designation { get; set; }
        public int? DepartmentId { get; set; }
        public string? AvatarPath { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public sealed class SeedUserRole
    {
        public string UserEmail { get; set; } = "";
        public string RoleName { get; set; } = "";
    }

    public sealed class SeedSheet
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public int Category { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public string DescriptionHtml { get; set; } = "";
        public string? Summary { get; set; }
        public string? ActionItems { get; set; }
        public string? NextMeetingAgenda { get; set; }
        public string? AttachmentFileName { get; set; }
        public string? AttachmentStoredPath { get; set; }
        public bool IsConfidential { get; set; }
        public string CreatedByUserId { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = "Submitted";
        public Guid Token { get; set; }
        public int? DepartmentId { get; set; }
        public int? IntendedForDepartmentId { get; set; }
    }

    public sealed class SeedApprovalStep
    {
        public int Id { get; set; }
        public int MinuteSheetId { get; set; }
        public int StepIndex { get; set; }
        public int Action { get; set; }
        public int Status { get; set; }
        public DateTime? ActedAt { get; set; }
        public string ApproverEmail { get; set; } = "";
    }

    public sealed class SeedComment
    {
        public int Id { get; set; }
        public int MinuteSheetId { get; set; }
        public int? ApprovalStepId { get; set; }
        public string AuthorUserId { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public int Kind { get; set; }
        public string Body { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public sealed class SeedShare
    {
        public int Id { get; set; }
        public int MinuteSheetId { get; set; }
        public string SharedByUserId { get; set; } = "";
        public string SharedWithEmail { get; set; } = "";
        public DateTime SharedAt { get; set; }
    }

    public sealed class SeedSuggestion
    {
        public int Id { get; set; }
        public int MinuteSheetId { get; set; }
        public string AuthorUserId { get; set; } = "";
        public string AuthorName { get; set; } = "";
        public bool IsAllOk { get; set; }
        public string Body { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public sealed class SeedVocabularyTerm
    {
        public int Id { get; set; }
        public int Category { get; set; }
        public string Term { get; set; } = "";
        public string? Aliases { get; set; }
        public bool IsActive { get; set; }
    }
}
