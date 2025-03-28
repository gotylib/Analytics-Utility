using DbListener.Dal.Entityes;
using Microsoft.EntityFrameworkCore;


namespace DbListener.Dal
{
    public class ConnectionDbContext : DbContext
    {
        public DbSet<Connection> Connections { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string connectionDb = $"Filename={PathDB.GetPath("connections.db")}";
            optionsBuilder.UseSqlite(connectionDb);

            
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Connection>().HasKey(x => x.Id);
        }
    }
}
