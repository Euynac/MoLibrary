using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;
using BuildingBlocksPlatform.Repository;
using BuildingBlocksPlatform.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;
using Task = Test.MoLibrary.Repository.Task;

namespace Test.MoLibrary.Repository
{
    public class ComplexEntityDbContext : MoDbContext<ComplexEntityDbContext>
    {
        public DbSet<Department> Departments { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Task> Tasks { get; set; }
        
        public ComplexEntityDbContext(DbContextOptions<ComplexEntityDbContext> options, IMoServiceProvider serviceProvider)
            : base(options, serviceProvider)
        {
        }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Department
            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                
                // Configure DepartmentMetadata as owned type
                entity.OwnsOne(e => e.Metadata, metadata =>
                {
                    metadata.Property(m => m.Location).HasMaxLength(200);
                    // Store the dictionary as JSON
                    metadata.Property(m => m.ExtraInformation).HasColumnName("ExtraInformation");
                });
                
                // Configure one-to-many relationship with Employee
                entity.HasMany(d => d.Employees)
                      .WithOne(e => e.Department)
                      .HasForeignKey(e => e.DepartmentId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
            
            // Employee
            modelBuilder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Salary).HasColumnType("decimal(18,2)");
                
                // Configure ContactInformation as owned type
                entity.OwnsOne(e => e.ContactInfo, contact =>
                {
                    contact.Property(c => c.PhoneNumber).HasMaxLength(20);
                    contact.Property(c => c.Address).HasMaxLength(200);
                    contact.Property(c => c.City).HasMaxLength(100);
                    contact.Property(c => c.Country).HasMaxLength(100);
                    contact.Property(c => c.PostalCode).HasMaxLength(20);
                });
            });
            
            // Project
            modelBuilder.Entity<Project>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.Budget).HasColumnType("decimal(18,2)");
                
                // Configure many-to-many relationship with Employee
                entity.HasMany(p => p.Employees)
                      .WithMany(e => e.Projects)
                      .UsingEntity(j => j.ToTable("EmployeeProjects"));
                
                // Configure one-to-many relationship with Task
                entity.HasMany(p => p.Tasks)
                      .WithOne(t => t.Project)
                      .HasForeignKey(t => t.ProjectId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
            
            // Task
            modelBuilder.Entity<Task>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                
                // Configure optional relationship with Employee (AssignedTo)
                entity.HasOne(t => t.AssignedTo)
                      .WithMany()
                      .HasForeignKey(t => t.AssignedToId)
                      .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
} 