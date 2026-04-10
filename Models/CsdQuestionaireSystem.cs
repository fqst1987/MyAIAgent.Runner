using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

[Table("csd_questionaire_systems")]
[Index(nameof(QuestionaireType), nameof(Code), nameof(CompanyId), nameof(Year), IsUnique = true)] // Composite unique index for the combination
public class CsdQuestionaireSystems
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Comment("主鍵 ID")]
    public int Id { get; set; }

    [Required]
    [Column("questionaire_type", TypeName = "varchar(1)")]
    [Comment("問卷大項")]
    public string QuestionaireType { get; set; } = string.Empty;

    [Required]
    [Column("code", TypeName = "varchar(50)")]
    [Comment("系統代號")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Column("company_id", TypeName = "varchar(20)")]
    [Comment("公司編號")]
    public string CompanyId { get; set; } = string.Empty;

    [Required]
    [Column("year")]
    [Comment("年度")]
    public int Year { get; set; }

    [Required]
    [Column("name", TypeName = "nvarchar(50)")]
    [Comment("問卷大項名稱")]
    public string Name { get; set; } = string.Empty;

    [Column("description", TypeName = "nvarchar(max)")]
    [Comment("備註")]
    public string? Description { get; set; }

    [Column("createuserid", TypeName = "varchar(20)")]
    [Comment("建立人員")]
    public string? CreateUserId { get; set; }

    [Column("createdt", TypeName = "datetime2")]
    [Comment("建立時間")]
    public DateTime? CreateDt { get; set; }

    [Column("updateuserid", TypeName = "varchar(20)")]
    [Comment("更新人員")]
    public string? UpdateUserId { get; set; }

    [Column("updatedt", TypeName = "datetime2")]
    [Comment("更新時間")]
    public DateTime? UpdateDt { get; set; }

    // Navigation property for the N:1 relationship
    [ForeignKey(nameof(QuestionaireType))]
    public virtual CsdQuestionaire? Questionaire { get; set; }
}