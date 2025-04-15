using System.Collections;
using System.ComponentModel.DataAnnotations;


namespace DbListener.Dal.Entityes
{
    public class Connection
    {
        [Key]
        public int Id;

        [MaxLength(100)]
        public string ConnectionName { get; set; }

        [MaxLength(100)]
        public string Type { get; set; }

        [MaxLength(100)]
        public string Url { get; set; }

        [MaxLength(100)]
        public string DbName { get; set; }

        [MaxLength(100)]
        public string Port { get; set; }

        [MaxLength(100)]
        public string Name { get; set; }

        public int WorkTime { get; set; }

        public int Wait { get; set; }

        [MaxLength(100)]
        public string Password { get; set; }

        public ICollection<Report> Reports { get; set; }
        
        public ICollection<SqlNoise> SqlNoise { get; set; }
    }
}
