using System.ComponentModel.DataAnnotations;

namespace minutesheet.Data;

public enum VocabularyCategory
{
    Person,
    Product,
    Platform,
    Technology,
    Abbreviation
}

public class DomainVocabularyTerm
{
    [Key]
    public int Id { get; set; }

    public VocabularyCategory Category { get; set; }

    [Required]
    [MaxLength(100)]
    public string Term { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Aliases { get; set; }

    public bool IsActive { get; set; } = true;
}
