using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;
using BuildingBlocksPlatform.Repository;
using BuildingBlocksPlatform.Repository.EntityInterfaces;
using BuildingBlocksPlatform.Repository.Interfaces;
using BuildingBlocksPlatform.Transaction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Test.MoLibrary.Repository
{
    [TestFixture]
    public class MoRepositoryTests
    {
        private Mock<IDbContextProvider<TestDbContext>> _dbContextProviderMock;
        private Mock<IMoServiceProvider> _serviceProviderMock;
        private Mock<IMoUnitOfWorkManager> _unitOfWorkManagerMock;
        private Mock<IMoUnitOfWork> _unitOfWorkMock;
        private Mock<IServiceProvider> _serviceProviderFactoryMock;
        private Mock<ILogger<MoRepositoryBase<TestEntity>>> _loggerMock;
        private TestRepository _repository;
        private TestDbContext _dbContext;
        private DbContextOptions<TestDbContext> _dbContextOptions;
        
        [SetUp]
        public void Setup()
        {
            // Setup in-memory database
            _dbContextOptions = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;

            // Setup mocks
            _dbContextProviderMock = new Mock<IDbContextProvider<TestDbContext>>();
            _serviceProviderMock = new Mock<IMoServiceProvider>();
            _unitOfWorkManagerMock = new Mock<IMoUnitOfWorkManager>();
            _unitOfWorkMock = new Mock<IMoUnitOfWork>();
            _serviceProviderFactoryMock = new Mock<IServiceProvider>();
            _loggerMock = new Mock<ILogger<MoRepositoryBase<TestEntity>>>();

            _dbContext = new TestDbContext(_dbContextOptions, _serviceProviderMock.Object);
            // Setup dbContextProvider to return our in-memory context
            _dbContextProviderMock.Setup(x => x.GetDbContextAsync())
                .ReturnsAsync(_dbContext);

            // Setup service provider to return logger and unit of work manager
            _serviceProviderFactoryMock.Setup(x => x.GetService(typeof(ILogger<MoRepositoryBase<TestEntity>>)))
                .Returns(_loggerMock.Object);

            _serviceProviderFactoryMock.Setup(x => x.GetService(typeof(IMoUnitOfWorkManager)))
                .Returns(_unitOfWorkManagerMock.Object);

            // Setup unit of work manager to return current unit of work
            _unitOfWorkManagerMock.Setup(x => x.Current)
                .Returns(_unitOfWorkMock.Object);

            _serviceProviderMock.Setup(x => x.ServiceProvider)
                .Returns(_serviceProviderFactoryMock.Object);

            // Create repository with mocked dependencies
            _repository = new TestRepository(_dbContextProviderMock.Object)
            {
                MoProvider = _serviceProviderMock.Object
            };
        }
        
        [TearDown]
        public void TearDown()
        {
            _dbContext.Database.EnsureDeleted();
            _dbContext.Dispose();
        }
        
        [Test]
        public async Task InsertAsync_ShouldAddEntityToDatabase()
        {
            // Arrange
            var entity = new TestEntity { Name = "Test Entity" };
            
            // Act
            var result = await _repository.InsertAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.Not.EqualTo(default(Guid)));
            
            var savedEntity = await _dbContext.TestEntities.FindAsync(result.Id);
            Assert.That(savedEntity, Is.Not.Null);
            Assert.That(savedEntity.Name, Is.EqualTo("Test Entity"));
        }
        
        [Test]
        public async Task InsertManyAsync_ShouldAddMultipleEntitiesToDatabase()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Entity 1" },
                new TestEntity { Name = "Entity 2" },
                new TestEntity { Name = "Entity 3" }
            };
            
            // Act
            await _repository.InsertManyAsync(entities);
            await _dbContext.SaveChangesAsync();
            
            // Assert
            var savedEntities = await _dbContext.TestEntities.ToListAsync();
            Assert.That(savedEntities, Is.Not.Null);
            Assert.That(savedEntities.Count, Is.EqualTo(3));
            Assert.That(savedEntities.Select(e => e.Name), Contains.Item("Entity 1"));
            Assert.That(savedEntities.Select(e => e.Name), Contains.Item("Entity 2"));
            Assert.That(savedEntities.Select(e => e.Name), Contains.Item("Entity 3"));
        }
        
        [Test]
        public async Task GetAsync_WithId_ShouldReturnEntityWhenExists()
        {
            // Arrange
            var entity = new TestEntity { Name = "Test Entity" };
            await _dbContext.TestEntities.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            // Act
            var result = await _repository.GetAsync(entity.Id);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(entity.Id));
            Assert.That(result.Name, Is.EqualTo("Test Entity"));
        }
        
        [Test]
        public async Task GetAsync_WithPredicate_ShouldReturnEntityWhenExists()
        {
            // Arrange
            var entity = new TestEntity { Name = "Test Entity" };
            await _dbContext.TestEntities.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            // Act
            var result = await _repository.GetAsync(e => e.Name == "Test Entity");
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(entity.Id));
            Assert.That(result.Name, Is.EqualTo("Test Entity"));
        }
        
        [Test]
        public async Task GetListAsync_ShouldReturnAllEntities()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Entity 1" },
                new TestEntity { Name = "Entity 2" },
                new TestEntity { Name = "Entity 3" }
            };
            
            await _dbContext.TestEntities.AddRangeAsync(entities);
            await _dbContext.SaveChangesAsync();
            
            // Act
            var result = await _repository.GetListAsync();
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result.Select(e => e.Name), Contains.Item("Entity 1"));
            Assert.That(result.Select(e => e.Name), Contains.Item("Entity 2"));
            Assert.That(result.Select(e => e.Name), Contains.Item("Entity 3"));
        }
        
        [Test]
        public async Task GetCountAsync_ShouldReturnCorrectCount()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Entity 1" },
                new TestEntity { Name = "Entity 2" },
                new TestEntity { Name = "Entity 3" }
            };
            
            await _dbContext.TestEntities.AddRangeAsync(entities);
            await _dbContext.SaveChangesAsync();
            
            // Act
            var count = await _repository.GetCountAsync();
            
            // Assert
            Assert.That(count, Is.EqualTo(3));
        }
        
        [Test]
        public async Task GetListAsync_WithPredicate_ShouldReturnFilteredEntities()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Entity 1" },
                new TestEntity { Name = "Entity 2" },
                new TestEntity { Name = "Other Entity" }
            };
            
            await _dbContext.TestEntities.AddRangeAsync(entities);
            await _dbContext.SaveChangesAsync();
            
            // Act
            var result = await _repository.GetListAsync(e => e.Name.StartsWith("Entity"));
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.Select(e => e.Name), Contains.Item("Entity 1"));
            Assert.That(result.Select(e => e.Name), Contains.Item("Entity 2"));
            Assert.That(result.Select(e => e.Name), Does.Not.Contain("Other Entity"));
        }
        
        [Test]
        public async Task FindAsync_WithId_ShouldReturnEntityWhenExists()
        {
            // Arrange
            var entity = new TestEntity { Name = "Test Entity" };
            await _dbContext.TestEntities.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            // Act
            var result = await _repository.FindAsync(entity.Id);
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(entity.Id));
            Assert.That(result.Name, Is.EqualTo("Test Entity"));
        }
        
        [Test]
        public async Task FindAsync_WithId_ShouldReturnNullWhenNotExists()
        {
            // Act
            var result = await _repository.FindAsync(Guid.NewGuid());
            
            // Assert
            Assert.That(result, Is.Null);
        }
        
        [Test]
        public async Task FindAsync_WithPredicate_ShouldReturnEntityWhenExists()
        {
            // Arrange
            var entity = new TestEntity { Name = "Test Entity" };
            await _dbContext.TestEntities.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            // Act
            var result = await _repository.FindAsync(e => e.Name == "Test Entity");
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.EqualTo(entity.Id));
            Assert.That(result.Name, Is.EqualTo("Test Entity"));
        }
        
        [Test]
        public async Task FindAsync_WithPredicate_ShouldReturnNullWhenNotExists()
        {
            // Act
            var result = await _repository.FindAsync(e => e.Name == "Non-Existent Entity");
            
            // Assert
            Assert.That(result, Is.Null);
        }
        
        [Test]
        public async Task UpdateAsync_ShouldUpdateEntityInDatabase()
        {
            // Arrange
            var entity = new TestEntity { Name = "Original Name" };
            await _dbContext.TestEntities.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            entity.Name = "Updated Name";
            
            // Act
            await _repository.UpdateAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            // Assert
            var updatedEntity = await _dbContext.TestEntities.FindAsync(entity.Id);
            Assert.That(updatedEntity, Is.Not.Null);
            Assert.That(updatedEntity.Name, Is.EqualTo("Updated Name"));
        }
        
        [Test]
        public async Task UpdateManyAsync_ShouldUpdateMultipleEntitiesInDatabase()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Original 1" },
                new TestEntity { Name = "Original 2" },
                new TestEntity { Name = "Original 3" }
            };
            
            await _dbContext.TestEntities.AddRangeAsync(entities);
            await _dbContext.SaveChangesAsync();
            
            // Modify entities
            foreach (var entity in entities)
            {
                entity.Name = $"Updated {entity.Name.Split(' ')[1]}";
            }
            
            // Act
            await _repository.UpdateManyAsync(entities);
            await _dbContext.SaveChangesAsync();
            
            // Assert
            var updatedEntities = await _dbContext.TestEntities.ToListAsync();
            Assert.That(updatedEntities, Is.Not.Null);
            Assert.That(updatedEntities.Count, Is.EqualTo(3));
            Assert.That(updatedEntities.Select(e => e.Name), Contains.Item("Updated 1"));
            Assert.That(updatedEntities.Select(e => e.Name), Contains.Item("Updated 2"));
            Assert.That(updatedEntities.Select(e => e.Name), Contains.Item("Updated 3"));
        }
        
        [Test]
        public async Task DeleteAsync_WithEntity_ShouldRemoveEntityFromDatabase()
        {
            // Arrange
            var entity = new TestEntity { Name = "Test Entity" };
            await _dbContext.TestEntities.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            // Act
            await _repository.DeleteAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            // Assert
            var deletedEntity = await _dbContext.TestEntities.FindAsync(entity.Id);
            Assert.That(deletedEntity, Is.Null);
        }
        
        [Test]
        public async Task DeleteAsync_WithId_ShouldRemoveEntityFromDatabase()
        {
            // Arrange
            var entity = new TestEntity { Name = "Test Entity" };
            await _dbContext.TestEntities.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            // Act
            await _repository.DeleteAsync(entity.Id);
            await _dbContext.SaveChangesAsync();
            
            // Assert
            var deletedEntity = await _dbContext.TestEntities.FindAsync(entity.Id);
            Assert.That(deletedEntity, Is.Null);
        }
        
        [Test]
        public async Task DeleteAsync_WithPredicate_ShouldRemoveMatchingEntitiesFromDatabase()
        {
            // Arrange
            var entities = new List<TestEntity>
            {
                new TestEntity { Name = "Delete Me 1" },
                new TestEntity { Name = "Delete Me 2" },
                new TestEntity { Name = "Keep Me" }
            };
            
            await _dbContext.TestEntities.AddRangeAsync(entities);
            await _dbContext.SaveChangesAsync();
            
            // Act
            await _repository.DeleteAsync(e => e.Name.StartsWith("Delete Me"));
            await _dbContext.SaveChangesAsync();
            
            // Assert
            var remainingEntities = await _dbContext.TestEntities.ToListAsync();
            Assert.That(remainingEntities.Count, Is.EqualTo(1));
            Assert.That(remainingEntities[0].Name, Is.EqualTo("Keep Me"));
        }
        
        [Test]
        public async Task DeleteManyAsync_WithEntities_ShouldRemoveMultipleEntitiesFromDatabase()
        {
            // Arrange
            var entitiesToKeep = new List<TestEntity>
            {
                new TestEntity { Name = "Keep Me 1" },
                new TestEntity { Name = "Keep Me 2" }
            };
            
            var entitiesToDelete = new List<TestEntity>
            {
                new TestEntity { Name = "Delete Me 1" },
                new TestEntity { Name = "Delete Me 2" }
            };
            
            await _dbContext.TestEntities.AddRangeAsync(entitiesToKeep);
            await _dbContext.TestEntities.AddRangeAsync(entitiesToDelete);
            await _dbContext.SaveChangesAsync();
            
            // Act
            await _repository.DeleteManyAsync(entitiesToDelete);
            await _dbContext.SaveChangesAsync();
            
            // Assert
            var remainingEntities = await _dbContext.TestEntities.ToListAsync();
            Assert.That(remainingEntities.Count, Is.EqualTo(2));
            Assert.That(remainingEntities.Select(e => e.Name), Contains.Item("Keep Me 1"));
            Assert.That(remainingEntities.Select(e => e.Name), Contains.Item("Keep Me 2"));
        }
        
        [Test]
        public async Task DeleteManyAsync_WithIds_ShouldRemoveMultipleEntitiesFromDatabase()
        {
            // Arrange
            var entitiesToKeep = new List<TestEntity>
            {
                new TestEntity { Name = "Keep Me 1" },
                new TestEntity { Name = "Keep Me 2" }
            };
            
            var entitiesToDelete = new List<TestEntity>
            {
                new TestEntity { Name = "Delete Me 1" },
                new TestEntity { Name = "Delete Me 2" }
            };
            
            await _dbContext.TestEntities.AddRangeAsync(entitiesToKeep);
            await _dbContext.TestEntities.AddRangeAsync(entitiesToDelete);
            await _dbContext.SaveChangesAsync();
            
            var idsToDelete = entitiesToDelete.Select(e => e.Id).ToList();
            
            // Act
            await _repository.DeleteManyAsync(idsToDelete);
            await _dbContext.SaveChangesAsync();
            
            // Assert
            var remainingEntities = await _dbContext.TestEntities.ToListAsync();
            Assert.That(remainingEntities.Count, Is.EqualTo(2));
            Assert.That(remainingEntities.Select(e => e.Name), Contains.Item("Keep Me 1"));
            Assert.That(remainingEntities.Select(e => e.Name), Contains.Item("Keep Me 2"));
        }
        
        [Test]
        public async Task ExistAsync_ShouldReturnTrueWhenEntityExists()
        {
            // Arrange
            var entity = new TestEntity { Name = "Test Entity" };
            await _dbContext.TestEntities.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            // Act
            var result = await _repository.ExistAsync(entity.Id);
            
            // Assert
            Assert.That(result, Is.True);
        }
        
        [Test]
        public async Task ExistAsync_ShouldReturnFalseWhenEntityDoesNotExist()
        {
            // Act
            var result = await _repository.ExistAsync(Guid.NewGuid());
            
            // Assert
            Assert.That(result, Is.False);
        }
        
        [Test]
        public async Task WithDetailsAsync_ShouldReturnQueryableWithIncludedDetails()
        {
            // This test requires mocking the navigation properties which is complex in an in-memory database
            // Generally this would be tested with a more elaborate setup including related entities
            
            // Arrange
            var entity = new TestEntity { Name = "Test Entity" };
            await _dbContext.TestEntities.AddAsync(entity);
            await _dbContext.SaveChangesAsync();
            
            // Act
            var queryable = await _repository.WithDetailsAsync();
            var result = await queryable.ToListAsync();
            
            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Name, Is.EqualTo("Test Entity"));
        }
    }
    
    // Helper classes for testing
    
    public class TestEntity : IMoEntity<Guid>
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public string Name { get; set; }
        
        public object[] GetKeys()
        {
            return [Id];
        }
        
        public void AutoSetNewId(bool notSetWhenNotDefault = false)
        {
            if (notSetWhenNotDefault && Id != default)
                return;
                
            Id = Guid.NewGuid();
        }
        
        public void SetNewId(Guid key)
        {
            Id = key;
        }
    }
    
    public class TestDbContext(DbContextOptions<TestDbContext> options, IMoServiceProvider serviceProvider)
        : MoDbContext<TestDbContext>(options, serviceProvider)
    {
        public DbSet<TestEntity> TestEntities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<TestEntity>().HasKey(e => e.Id);
        }
    }
    
    public class TestRepository(IDbContextProvider<TestDbContext> dbContextProvider)
        : MoRepository<TestDbContext, TestEntity, Guid>(dbContextProvider);
} 