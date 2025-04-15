using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MoLibrary.Core.GlobalJson.Attributes;
using MoLibrary.Repository.EFCoreExtensions;
using NUnit.Framework;

namespace Test.MoLibrary.Core.EnumFormatValue;

/// <summary>
/// Tests for the EnumFormatValueConverter and related classes.
/// </summary>
[TestFixture]
public class EnumFormatValueConverterTests
{
    /// <summary>
    /// Test enum that uses EnumFormatValueAttribute.
    /// </summary>
    public enum TestStatus
    {
        /// <summary>
        /// Unknown value without format attribute.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Valid value with "V" format.
        /// </summary>
        [EnumFormatValue("V")]
        Valid = 1,

        /// <summary>
        /// Failed value with "F" format.
        /// </summary>
        [EnumFormatValue("F")]
        Failed = 2,

        /// <summary>
        /// Value with special characters in the format.
        /// </summary>
        [EnumFormatValue("V/F")]
        ValidOrFailed = 3
    }

    /// <summary>
    /// Test entity class.
    /// </summary>
    public class TestEntity
    {
        /// <summary>
        /// Gets or sets the entity ID.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        public TestStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the nullable status.
        /// </summary>
        public TestStatus? NullableStatus { get; set; }
    }

    /// <summary>
    /// Test database context.
    /// </summary>
    public class TestDbContext : DbContext
    {
        /// <summary>
        /// Gets or sets the test entities.
        /// </summary>
        public DbSet<TestEntity> TestEntities { get; set; } = null!;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestDbContext"/> class.
        /// </summary>
        /// <param name="options">The database context options.</param>
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Configures the model.
        /// </summary>
        /// <param name="modelBuilder">The model builder.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestEntity>(builder =>
            {
                builder.Property(e => e.Status)
                    .HasEnumFormatValueConversion()
                    .HasMaxLength(10);

                builder.Property(e => e.NullableStatus)
                    .HasEnumFormatValueConversion()
                    .HasMaxLength(10);
            });
        }
    }

    /// <summary>
    /// Test saving enum values to the database and loading them back with EnumFormatValueConverter.
    /// </summary>
    [Test]
    public void EnumFormatValueConverter_ShouldConvertBetweenEnumAndFormattedString()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"EnumFormatValueConverterTests-{Guid.NewGuid()}")
            .Options;

        using (var context = new TestDbContext(options))
        {
            // Act - Save entity with enum values
            var entity = new TestEntity
            {
                Id = 1,
                Status = TestStatus.Valid,
                NullableStatus = TestStatus.Failed
            };

            context.TestEntities.Add(entity);
            context.SaveChanges();
        }

        // Assert - Verify values are converted correctly when loaded
        using (var context = new TestDbContext(options))
        {
            var loadedEntity = context.TestEntities.Single(e => e.Id == 1);
            
            loadedEntity.Status.Should().Be(TestStatus.Valid);
            loadedEntity.NullableStatus.Should().Be(TestStatus.Failed);
        }
    }

    /// <summary>
    /// Test saving a null enum value to the database and loading it back with EnumFormatValueConverter.
    /// </summary>
    [Test]
    public void EnumFormatValueConverter_ShouldHandleNullValues()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"EnumFormatValueConverterTests-Null-{Guid.NewGuid()}")
            .Options;

        using (var context = new TestDbContext(options))
        {
            // Act - Save entity with null enum value
            var entity = new TestEntity
            {
                Id = 1,
                Status = TestStatus.Valid,
                NullableStatus = null
            };

            context.TestEntities.Add(entity);
            context.SaveChanges();
        }

        // Assert - Verify null values are handled correctly when loaded
        using (var context = new TestDbContext(options))
        {
            var loadedEntity = context.TestEntities.Single(e => e.Id == 1);
            
            loadedEntity.Status.Should().Be(TestStatus.Valid);
            loadedEntity.NullableStatus.Should().BeNull();
        }
    }

    /// <summary>
    /// Test updating an enum value in the database and loading it back with EnumFormatValueConverter.
    /// </summary>
    [Test]
    public void EnumFormatValueConverter_ShouldHandleUpdates()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase($"EnumFormatValueConverterTests-Update-{Guid.NewGuid()}")
            .Options;

        using (var context = new TestDbContext(options))
        {
            // Create initial entity
            var entity = new TestEntity
            {
                Id = 1,
                Status = TestStatus.Valid,
                NullableStatus = TestStatus.Failed
            };

            context.TestEntities.Add(entity);
            context.SaveChanges();
        }

        // Act - Update entity
        using (var context = new TestDbContext(options))
        {
            var entity = context.TestEntities.Single(e => e.Id == 1);
            entity.Status = TestStatus.ValidOrFailed;
            entity.NullableStatus = null;
            context.SaveChanges();
        }

        // Assert - Verify updates are handled correctly
        using (var context = new TestDbContext(options))
        {
            var loadedEntity = context.TestEntities.Single(e => e.Id == 1);
            
            loadedEntity.Status.Should().Be(TestStatus.ValidOrFailed);
            loadedEntity.NullableStatus.Should().BeNull();
        }
    }
} 