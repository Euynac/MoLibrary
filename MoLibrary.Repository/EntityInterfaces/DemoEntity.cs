using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoLibrary.Repository.EntityInterfaces;

/// <summary>
/// A demonstration entity that shows how to use IHasEntityConfig interface
/// to configure entities directly within the entity class.
/// </summary>
/// <remarks>
/// This is a demo implementation for reference only.
/// </remarks>
public class DemoEntity : MoEntity<long>, IHasEntityConfig<DemoEntity>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DemoEntity"/> class.
    /// </summary>
    public DemoEntity()
    {
        Items = new List<DemoItem>();
    }

    /// <summary>
    /// Gets or sets the entity's title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the entity's description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the creation date of the entity.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the status of the entity.
    /// </summary>
    public DemoStatus Status { get; set; } = DemoStatus.Draft;

    /// <summary>
    /// Gets or sets the related items collection.
    /// </summary>
    public List<DemoItem> Items { get; set; }

    /// <summary>
    /// Configures the entity using EF Core's fluent API.
    /// This method is automatically discovered and called during OnModelCreating.
    /// </summary>
    /// <param name="builder">The entity type builder used to configure this entity</param>
    public void Configure(EntityTypeBuilder<DemoEntity> builder)
    {
        // Table configuration
        builder.ToTable("Demos");

        // Primary key
        builder.HasKey(e => e.Id);

        // Properties configuration
        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.CreatedDate)
            .IsRequired();

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        // Indexes
        builder.HasIndex(e => e.Title);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CreatedDate);

        // Relationships
        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey("DemoEntityId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// A related item for the demo entity.
/// </summary>
public class DemoItem : MoEntity<long>
{
    /// <summary>
    /// Gets or sets the name of the item.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the value of the item.
    /// </summary>
    public decimal Value { get; set; }
}

/// <summary>
/// Status options for the demo entity.
/// </summary>
public enum DemoStatus
{
    /// <summary>
    /// Draft status
    /// </summary>
    Draft,

    /// <summary>
    /// Active status
    /// </summary>
    Active,

    /// <summary>
    /// Completed status
    /// </summary>
    Completed,

    /// <summary>
    /// Archived status
    /// </summary>
    Archived
} 