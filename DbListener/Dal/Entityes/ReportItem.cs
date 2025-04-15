namespace DbListener.Dal.Entityes;

public class ReportItem
{
    long Id { get; set; }
    public string Query  { get; set; }
    
    public string TablesAndColumns { get; set; }
    
    public Report Report { get; set; }
}