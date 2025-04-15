namespace DbListener.Dal.Entityes;

public class Report
{
    public int Id { get; set; }
    
    public DateTime DateOfLog { get; set; }
    
    public int ConnectionId { get; set; }
    
    public Connection Connection { get; set; }
    
    public ICollection<ReportItem> ReportItems { get; set; }
}