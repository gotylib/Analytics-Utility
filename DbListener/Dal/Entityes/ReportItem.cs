using System.ComponentModel.DataAnnotations;

namespace DbListener.Dal.Entityes;

public class ReportItem
{
    [Key]
    public long Id { get; set; }
    public string Query  { get; set; }
    
    public string TablesAndColumns { get; set; }
    
    public Report Report { get; set; }
}