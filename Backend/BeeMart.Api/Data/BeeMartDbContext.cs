using BeeMart.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BeeMart.Api.Data;

public class BeeMartDbContext : DbContext
{
    public BeeMartDbContext(
        DbContextOptions<BeeMartDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<TransactionDetail> TransactionDetails => Set<TransactionDetail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("MsUser");

            entity.HasKey(u => u.IdUser);

            entity.Property(u => u.IdUser)
                .HasMaxLength(5)
                .IsRequired();

            entity.Property(u => u.Username)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(u => u.Username)
                .IsUnique();

            entity.Property(u => u.FullName)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(u => u.PasswordHash)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(u => u.Role)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(u => u.Userin)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(u => u.Userup)
                .HasMaxLength(36);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("MsProduct");

            entity.HasKey(p => p.IdProduct);

            entity.Property(p => p.IdProduct)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(p => p.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(p => p.Weight)
                .HasColumnType("decimal(10,2)");

            entity.Property(p => p.Price)
                .HasColumnType("decimal(18,2)");

            entity.Property(p => p.Discount)
                .HasColumnType("decimal(5,2)")
                .HasDefaultValue(0m);

            entity.HasIndex(p => p.Name);

            entity.Property(p => p.Userin)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(p => p.Userup)
                .HasMaxLength(36);
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.ToTable("LtPaymentMethod");

            entity.HasKey(p => p.IdPaymentMethod);

            entity.Property(p => p.MethodName)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(p => p.Userin)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(p => p.Userup)
                .HasMaxLength(36);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("TrTransaction");

            entity.HasKey(t => t.IdTransaction);

            entity.Property(t => t.IdTransaction)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(t => t.InvoiceNo)
                .HasMaxLength(20)
                .IsRequired();

            entity.HasIndex(t => t.InvoiceNo)
                .IsUnique();

            entity.Property(t => t.IdUser)
                .HasMaxLength(5)
                .IsRequired();

            entity.Property(t => t.TotalPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(t => t.Userin)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(t => t.Userup)
                .HasMaxLength(36);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(t => t.IdUser)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<PaymentMethod>()
                .WithMany()
                .HasForeignKey(t => t.IdPaymentMethod)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(t => t.Details)
                .WithOne()
                .HasForeignKey(d => d.IdTransaction)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TransactionDetail>(entity =>
        {
            entity.ToTable("TrTransactionDetail");

            entity.HasKey(d => d.IdTransactionDetail);

            entity.Property(d => d.IdTransactionDetail)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(d => d.IdTransaction)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(d => d.IdProduct)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(d => d.Price)
                .HasColumnType("decimal(18,2)");

            entity.Property(d => d.Discount)
                .HasColumnType("decimal(5,2)")
                .HasDefaultValue(0m);

            entity.Property(d => d.SubTotal)
                .HasColumnType("decimal(18,2)");

            entity.Property(d => d.Userin)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(d => d.Userup)
                .HasMaxLength(36);

            entity.HasOne<Product>()
                .WithMany()
                .HasForeignKey(d => d.IdProduct)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}