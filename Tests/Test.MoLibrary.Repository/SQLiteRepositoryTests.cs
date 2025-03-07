using BuildingBlocksPlatform.Repository;
using BuildingBlocksPlatform.Repository.Interfaces;
using BuildingBlocksPlatform.Transaction;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Data.Common;
using BuildingBlocksPlatform.DependencyInjection.AppInterfaces;

namespace Test.MoLibrary.Repository
{
    [TestFixture]
    public class SQLiteRepositoryTests
    {
        private Mock<IDbContextProvider<ComplexEntityDbContext>> _dbContextProviderMock;
        private Mock<IMoServiceProvider> _serviceProviderMock;
        private Mock<IMoUnitOfWorkManager> _unitOfWorkManagerMock;
        private Mock<IMoUnitOfWork> _unitOfWorkMock;
        private Mock<IServiceProvider> _serviceProviderFactoryMock;
        private Mock<ILogger<MoRepositoryBase<Department>>> _loggerMock;
        
        private DepartmentRepository _departmentRepository;
        private EmployeeRepository _employeeRepository;
        
        private ComplexEntityDbContext _dbContext;
        private DbContextOptions<ComplexEntityDbContext> _dbContextOptions;
        private DbConnection _connection;
        
        [SetUp]
        public void Setup()
        {
            // Create SQLite in-memory connection
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();
            
            // Configure the options to use SQLite with the in-memory connection
            _dbContextOptions = new DbContextOptionsBuilder<ComplexEntityDbContext>()
                .UseSqlite(_connection)
                .Options;
            
            // Setup service provider mock for DbContext
            _serviceProviderMock = new Mock<IMoServiceProvider>();
            _dbContext = new ComplexEntityDbContext(_dbContextOptions, _serviceProviderMock.Object);
            
            // Create the schema in the database
            _dbContext.Database.EnsureCreated();
            
            // Setup mocks
            _dbContextProviderMock = new Mock<IDbContextProvider<ComplexEntityDbContext>>();
            _unitOfWorkManagerMock = new Mock<IMoUnitOfWorkManager>();
            _unitOfWorkMock = new Mock<IMoUnitOfWork>();
            _serviceProviderFactoryMock = new Mock<IServiceProvider>();
            _loggerMock = new Mock<ILogger<MoRepositoryBase<Department>>>();
            
            // Setup dbContextProvider to return our SQLite context
            _dbContextProviderMock.Setup(x => x.GetDbContextAsync())
                .ReturnsAsync(_dbContext);
            
            // Setup service provider to return logger and unit of work manager
            _serviceProviderFactoryMock.Setup(x => x.GetService(typeof(ILogger<MoRepositoryBase<Department>>)))
                .Returns(_loggerMock.Object);
            
            _serviceProviderFactoryMock.Setup(x => x.GetService(typeof(IMoUnitOfWorkManager)))
                .Returns(_unitOfWorkManagerMock.Object);
            
            // Setup unit of work manager to return current unit of work
            _unitOfWorkManagerMock.Setup(x => x.Current)
                .Returns(_unitOfWorkMock.Object);
            
            _serviceProviderMock.Setup(x => x.ServiceProvider)
                .Returns(_serviceProviderFactoryMock.Object);
            
            // Create repositories with mocked dependencies
            _departmentRepository = new DepartmentRepository(_dbContextProviderMock.Object)
            {
                MoProvider = _serviceProviderMock.Object
            };
            
            _employeeRepository = new EmployeeRepository(_dbContextProviderMock.Object)
            {
                MoProvider = _serviceProviderMock.Object
            };
        }
        
        [TearDown]
        public void TearDown()
        {
            _dbContext.Dispose();
            _connection.Dispose();
        }
        
        [Test]
        public async Task SQLite_InsertAndRetrieve_ShouldWorkCorrectly()
        {
            // Arrange
            var department = new Department
            {
                Name = "IT Department",
                Description = "Information Technology"
            };
            
            // Act - Insert
            var result = await _departmentRepository.InsertAsync(department);
            await _dbContext.SaveChangesAsync();
            
            // Act - Retrieve
            var savedDepartment = await _departmentRepository.GetAsync(department.Id);
            
            // Assert
            Assert.That(savedDepartment, Is.Not.Null);
            Assert.That(savedDepartment.Id, Is.EqualTo(department.Id));
            Assert.That(savedDepartment.Name, Is.EqualTo("IT Department"));
        }
        
        [Test]
        public async Task SQLite_ComplexQueries_ShouldWorkCorrectly()
        {
            // Arrange - Create departments with employees
            var departments = new List<Department>
            {
                new Department { Name = "HR" },
                new Department { Name = "Finance" },
                new Department { Name = "Operations" }
            };
            
            foreach (var dept in departments)
            {
                await _departmentRepository.InsertAsync(dept);
            }
            
            await _dbContext.SaveChangesAsync();
            
            var employees = new List<Employee>
            {
                new Employee 
                { 
                    FirstName = "John", 
                    LastName = "Smith", 
                    Email = "john.smith@example.com",
                    DepartmentId = departments[0].Id,
                    Salary = 65000
                },
                new Employee 
                { 
                    FirstName = "Jane", 
                    LastName = "Doe", 
                    Email = "jane.doe@example.com",
                    DepartmentId = departments[0].Id,
                    Salary = 70000
                },
                new Employee 
                { 
                    FirstName = "Mark", 
                    LastName = "Johnson", 
                    Email = "mark.johnson@example.com",
                    DepartmentId = departments[1].Id,
                    Salary = 80000
                },
                new Employee 
                { 
                    FirstName = "Lisa", 
                    LastName = "Brown", 
                    Email = "lisa.brown@example.com",
                    DepartmentId = departments[2].Id,
                    Salary = 75000
                }
            };
            
            foreach (var emp in employees)
            {
                await _employeeRepository.InsertAsync(emp);
            }
            
            await _dbContext.SaveChangesAsync();
            
            // Act - Complex query with join and where clause
            var queryable = await _employeeRepository.GetQueryableAsync();
            var hrEmployees = await queryable
                .Where(e => e.Department.Name == "HR" && e.Salary > 60000)
                .OrderByDescending(e => e.Salary)
                .ToListAsync();
            
            // Assert
            Assert.That(hrEmployees, Is.Not.Null);
            Assert.That(hrEmployees.Count, Is.EqualTo(2));
            Assert.That(hrEmployees[0].FirstName, Is.EqualTo("Jane")); // Higher salary should be first
            Assert.That(hrEmployees[1].FirstName, Is.EqualTo("John"));
        }
        
        [Test]
        public async Task SQLite_Transactions_ShouldWorkCorrectly()
        {
            // Arrange - Setup transaction mock
            _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback(() => _dbContext.SaveChanges());
                
            var department = new Department
            {
                Name = "Legal",
                Description = "Legal Department"
            };
            
            // Act - Use transaction via unit of work
            await _departmentRepository.InsertAsync(department, autoSave: true);
            
            // Verify the SaveChangesAsync was called
            _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            
            // Assert - Department should be saved
            var savedDepartment = await _dbContext.Departments.FindAsync(department.Id);
            Assert.That(savedDepartment, Is.Not.Null);
            Assert.That(savedDepartment.Name, Is.EqualTo("Legal"));
        }
        
        [Test]
        public async Task SQLite_ComplexGraph_ShouldWorkCorrectly()
        {
            // Arrange - Create a complex entity graph
            var department = new Department { Name = "Quality Assurance" };
            
            var employee = new Employee
            {
                FirstName = "David",
                LastName = "Miller",
                Email = "david.miller@example.com",
                Department = department,
                ContactInfo = new ContactInformation
                {
                    PhoneNumber = "123-456-7890",
                    Address = "456 Oak St",
                    City = "Chicago",
                    Country = "USA"
                }
            };
            
            // Act - Insert the graph
            await _departmentRepository.InsertAsync(department);
            await _employeeRepository.InsertAsync(employee);
            await _dbContext.SaveChangesAsync();
            
            // Clear change tracker to ensure fresh retrieval
            _dbContext.ChangeTracker.Clear();
            
            // Retrieve with navigation properties
            var queryable = await _employeeRepository.WithDetailsAsync();
            var retrievedEmployee = await queryable
                .FirstOrDefaultAsync(e => e.Email == "david.miller@example.com");
            
            // Assert
            Assert.That(retrievedEmployee, Is.Not.Null);
            Assert.That(retrievedEmployee.Department, Is.Not.Null);
            Assert.That(retrievedEmployee.Department.Name, Is.EqualTo("Quality Assurance"));
            Assert.That(retrievedEmployee.ContactInfo, Is.Not.Null);
            Assert.That(retrievedEmployee.ContactInfo.City, Is.EqualTo("Chicago"));
        }
        
        [Test]
        public async Task SQLite_DeleteDirect_ShouldBypassChangeTracker()
        {
            // Arrange - Create multiple departments
            var departments = new List<Department>
            {
                new Department { Name = "Dept 1" },
                new Department { Name = "Dept 2" },
                new Department { Name = "Dept 3" },
                new Department { Name = "Dept to Keep" }
            };
            
            foreach (var dept in departments)
            {
                await _departmentRepository.InsertAsync(dept);
            }
            
            await _dbContext.SaveChangesAsync();
            
            // Act - Use DeleteDirectAsync to bypass change tracker
            await _departmentRepository.DeleteDirectAsync(d => d.Name.StartsWith("Dept "));
            
            // Assert - Only "Dept to Keep" should remain
            var remainingDepartments = await _dbContext.Departments.ToListAsync();
            Assert.That(remainingDepartments.Count, Is.EqualTo(1));
            Assert.That(remainingDepartments[0].Name, Is.EqualTo("Dept to Keep"));
        }
        
        [Test]
        public async Task SQLite_TrackGraph_ShouldUpdateNestedEntities()
        {
            // Arrange - Create a complex entity with nested objects
            var department = new Department
            {
                Name = "R&D",
                Description = "Research and Development",
                Metadata = new DepartmentMetadata
                {
                    Location = "West Wing",
                    Floor = 2,
                }
            };
            
            await _departmentRepository.InsertAsync(department);
            await _dbContext.SaveChangesAsync();
            
            // Detach entities to simulate retrieval in a different context
            _dbContext.ChangeTracker.Clear();
            
            // Act - Load, modify nested object, and update
            var savedDepartment = await _departmentRepository.GetAsync(department.Id);
            savedDepartment.Metadata.Location = "East Wing";
            savedDepartment.Metadata.Floor = 3;
            
            // Update using TrackGraph
            var dbContext = await ((IMoRepository)_departmentRepository).GetDbContextAsync();
            dbContext.ChangeTracker.TrackGraph(savedDepartment, node =>
            {
                node.Entry.State = EntityState.Modified;
            });
            
            await _dbContext.SaveChangesAsync();
            
            // Assert - Verify nested objects were updated
            _dbContext.ChangeTracker.Clear();
            var updatedDepartment = await _dbContext.Departments.FindAsync(department.Id);
            
            Assert.That(updatedDepartment, Is.Not.Null);
            Assert.That(updatedDepartment.Metadata.Location, Is.EqualTo("East Wing"));
            Assert.That(updatedDepartment.Metadata.Floor, Is.EqualTo(3));
        }
    }
} 