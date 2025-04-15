namespace DbListener.Dal.Entityes;

public class SqlNoise
{
    long Id { get; set; }
    public string Query { get; set; }
    
    public Connection Connection { get; set; }
}