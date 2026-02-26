using System.ComponentModel.DataAnnotations.Schema;
namespace Stat.Models;
public class Rapport
{
    public int Id { get; set; }
    [ForeignKey("User")]
    public string User_id { get; set; }
    public string CodeStructure { get; set; } // The DIW/DRI/DC code
    public string Type { get; set; } // "Trimestriel" or "Annuel"
    public string Year { get; set; }
    public string FilePath { get; set; }
    public int Status { get; set; } // 0: En attente, 1: Validé, 2: Rejeté
    public string? Motif { get; set; } // Reason for rejection
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public virtual User User { get; set; }
}