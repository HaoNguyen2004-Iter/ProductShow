using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;
using SPMH.DBContext.Entities;

namespace SPMH.DBContext
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Brand> Brands => Set<Brand>();
        public DbSet<Account> Accounts => Set<Account>();

        public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }

        //Cấu hình EF Core bằng FluentAPI 
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Products", tb =>
                {
                    tb.HasTrigger("TR_Products_Insert");
                    tb.HasTrigger("TR_Products_Update");
                    tb.HasTrigger("TR_Products_Delete");
                });

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.PriceVnd).HasColumnType("decimal(18,2)").IsRequired();
                entity.Property(e => e.Stock).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.Url).HasMaxLength(500);
                entity.Property(e => e.Keyword).HasMaxLength(500);

                entity.HasOne(d => d.Brand)
                    .WithMany(p => p.Products)
                    .HasForeignKey(d => d.BrandId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Products_Brands_BrandId");

                entity.HasOne(d => d.CreateByAccount)
                    .WithMany()
                    .HasForeignKey(d => d.CreateBy)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Products_Accounts_CreateBy");

                entity.HasOne(d => d.UpdateByAccount)
                    .WithMany()
                    .HasForeignKey(d => d.UpdateBy)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Products_Accounts_UpdateBy");
            });

            modelBuilder.Entity<Brand>(entity =>
            {
                entity.ToTable("Brands");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<Account>(entity =>
            {
                entity.ToTable("Accounts");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Password).IsRequired().HasMaxLength(200);
            });
        }
    }
}