using System.ComponentModel.DataAnnotations;

namespace DbListener.Dal.Entityes;

public class SqlNoise
{
    [Key]
    public long Id { get; set; }
    public string Query { get; set; }
    
    public Connection Connection { get; set; }
}