using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace Rulesage.DslRetrieval.Database.Entities;

public class DslEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "text")]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public float Level { get; set; }

    [Required]
    public float Semanticity { get; set; }

    [Required]
    [Column(TypeName = "jsonb")]
    public object PartialAst { get; set; } = new();

    [Required]
    [Column(TypeName = "jsonb")]
    public object Context { get; set; } = new();

    [Required]
    [Column(TypeName = "jsonb")]
    public object[] Subtasks { get; set; } = [];

    [Required]
    [Column(TypeName = "vector(384)")]
    public required Vector Embedding { get; set; }
}