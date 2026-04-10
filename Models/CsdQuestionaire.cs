using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Table("csd_questionaires")]
[Index(nameof(Type), IsUnique = true)]
public class CsdQuestionaire
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Comment("主鍵 ID")]
    public int Id { get; set; }

    [Required]
    [Column(TypeName = "varchar(1)")]
    [Comment("問卷大項")]
    public string Type { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "nvarchar(50)")]
    [Comment("問卷大項名稱")]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(200)")]
    [Comment("URL")]
    public string? Url { get; set; }

    [Comment("是否啟用")]
    public bool Enabled { get; set; }

    public virtual ICollection<CsdQuestionaireSystems> CsdQuestionaireSystems { get; set; } = new List<CsdQuestionaireSystems>();
}