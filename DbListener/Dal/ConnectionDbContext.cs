using DbListener.Dal.Entityes;
using Microsoft.EntityFrameworkCore;


namespace DbListener.Dal
{
    public class ConnectionDbContext : DbContext
    {
        public DbSet<Connection> Connections { get; set; }
        
        public DbSet<Report> Reports { get; set; }
        
        public DbSet<ReportItem> ReportItems { get; set; }
        public DbSet<SqlNoise> SqlNoises { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string connectionDb = $"Filename={PathDB.GetPath("connections.db")}";
            optionsBuilder.UseSqlite(connectionDb);

            
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Connection>().HasKey(x => x.Id);
            modelBuilder.Entity<Report>().HasKey(x => x.Id);
            modelBuilder.Entity<SqlNoise>().HasKey(x => x.Id);
            modelBuilder.Entity<ReportItem>().HasKey(x => x.Id);
        }
    }
}
