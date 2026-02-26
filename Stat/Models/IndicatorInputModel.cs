public class IndicatorInputModel
{
    public string IndicatorId { get; set; }
    public string IntituleIn { get; set; } 
    public float? Numerateur { get; set; }
    public float? Denominateur { get; set; }
    public float? Taux { get; set; }
    public float? Ecart { get; set; }

    public float? cible { get; set; }
}