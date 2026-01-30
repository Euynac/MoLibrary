# Entity Self-Configuration with IHasEntitySelfConfig

The `IHasEntitySelfConfig<TEntity>` interface provides a convenient way to define entity configurations directly within the entity class itself, rather than:
1. Creating separate configuration classes that implement `IEntityTypeConfiguration<T>`
2. Configuring entities directly in the `DbContext.OnModelCreating` method

This approach keeps entity configuration code close to the entity definition, making it easier to maintain and understand.

## How to Use

1. Make your entity class implement `IHasEntitySelfConfig<YourEntityType>`
2. Implement the `Configure(EntityTypeBuilder<YourEntityType> builder)` method
3. Use the builder to configure your entity's properties, relationships, and constraints

## Example

```csharp
public class Product : MoEntity<long>, IHasEntitySelfConfig<Product>
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public ProductCategory Category { get; set; }
    
    // Implementation of IHasEntitySelfConfig
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        
        // Configure properties
        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);
            
        builder.Property(p => p.Price)
            .IsRequired()
            .HasPrecision(18, 2);
            
        builder.Property(p => p.Description)
            .HasMaxLength(500);
            
        builder.Property(p => p.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);
        
        // Configure indexes
        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.Category);
    }
}
```

## Benefits

- Encapsulation: Keep entity definitions and configurations together
- Maintainability: Easier to find and update configurations
- Organization: No need to create separate configuration classes
- Readability: Clear association between entity and its database mapping

## How It Works

The implementation uses reflection to discover entities that implement `IHasEntitySelfConfig<TEntity>` and automatically calls their `Configure` method during the `OnModelCreating` phase of the DbContext.

No additional setup is required beyond implementing the interface in your entity classes. 