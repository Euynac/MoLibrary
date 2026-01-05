using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.DependencyInjection.AppInterfaces;
using MoLibrary.Repository;
using MoLibrary.Repository.Interfaces;
using MoLibrary.Repository.Modules;
using MoLibrary.Repository.Transaction;
using Moq;

namespace Test.MoLibrary.Repository
{
    [TestFixture]
    public class ComplexEntityRepositoryTests
    {
        private Mock<IDbContextProvider<ComplexEntityDbContext>> _dbContextProviderMock;
        private Mock<IMoServiceProvider> _serviceProviderMock;
        private Mock<IMoUnitOfWorkManager> _unitOfWorkManagerMock;
        private Mock<IMoUnitOfWork> _unitOfWorkMock;
        private Mock<IServiceProvider> _serviceProviderFactoryMock;
        private Mock<ILogger<MoRepositoryBase<Department>>> _departmentLoggerMock;
        private Mock<ILogger<MoRepositoryBase<Employee>>> _employeeLoggerMock;
        private Mock<ILogger<MoRepositoryBase<Project>>> _projectLoggerMock;
        private Mock<ILogger<MoRepositoryBase<TaskEntity>>> _taskLoggerMock;

        private DepartmentRepository _departmentRepository;
        private EmployeeRepository _employeeRepository;
        private ProjectRepository _projectRepository;
        private TaskRepository _taskRepository;

        private ComplexEntityDbContext _dbContext;
        private DbContextOptions<ComplexEntityDbContext> _dbContextOptions;

        [SetUp]
        public void Setup()
        {
            // Setup in-memory database
            _dbContextOptions = new DbContextOptionsBuilder<ComplexEntityDbContext>()
                .UseInMemoryDatabase(databaseName: $"ComplexEntityTestDb_{Guid.NewGuid()}")
                .Options;

            // Setup mocks first
            _dbContextProviderMock = new Mock<IDbContextProvider<ComplexEntityDbContext>>();
            _serviceProviderMock = new Mock<IMoServiceProvider>();
            _unitOfWorkManagerMock = new Mock<IMoUnitOfWorkManager>();
            _unitOfWorkMock = new Mock<IMoUnitOfWork>();
            _serviceProviderFactoryMock = new Mock<IServiceProvider>();
            _departmentLoggerMock = new Mock<ILogger<MoRepositoryBase<Department>>>();
            _employeeLoggerMock = new Mock<ILogger<MoRepositoryBase<Employee>>>();
            _projectLoggerMock = new Mock<ILogger<MoRepositoryBase<Project>>>();
            _taskLoggerMock = new Mock<ILogger<MoRepositoryBase<TaskEntity>>>();

            // Setup service provider to return loggers and unit of work manager
            _serviceProviderFactoryMock.Setup(x => x.GetService(typeof(ILogger<MoRepositoryBase<Department>>)))
                .Returns(_departmentLoggerMock.Object);
            _serviceProviderFactoryMock.Setup(x => x.GetService(typeof(ILogger<MoRepositoryBase<Employee>>)))
                .Returns(_employeeLoggerMock.Object);
            _serviceProviderFactoryMock.Setup(x => x.GetService(typeof(ILogger<MoRepositoryBase<Project>>)))
                .Returns(_projectLoggerMock.Object);
            _serviceProviderFactoryMock.Setup(x => x.GetService(typeof(ILogger<MoRepositoryBase<TaskEntity>>)))
                .Returns(_taskLoggerMock.Object);

            _serviceProviderFactoryMock.Setup(x => x.GetService(typeof(IMoUnitOfWorkManager)))
                .Returns(_unitOfWorkManagerMock.Object);
            _serviceProviderFactoryMock.Setup(x => x.GetService(typeof(IOptions<ModuleRepositoryOption>)))
                .Returns(new OptionsWrapper<ModuleRepositoryOption>(new ModuleRepositoryOption()));
            
            // Setup unit of work manager to return current unit of work
            _unitOfWorkManagerMock.Setup(x => x.Current)
                .Returns(_unitOfWorkMock.Object);

            _serviceProviderMock.Setup(x => x.ServiceProvider)
                .Returns(_serviceProviderFactoryMock.Object);

            // Create DbContext AFTER service provider is properly configured
            _dbContext = new ComplexEntityDbContext(_dbContextOptions, _serviceProviderMock.Object);

            // Setup dbContextProvider to return our in-memory context
            _dbContextProviderMock.Setup(x => x.GetDbContextAsync())
                .ReturnsAsync(_dbContext);

            // Create repositories with mocked dependencies
            _departmentRepository = new DepartmentRepository(_dbContextProviderMock.Object)
            {
                MoProvider = _serviceProviderMock.Object
            };

            _employeeRepository = new EmployeeRepository(_dbContextProviderMock.Object)
            {
                MoProvider = _serviceProviderMock.Object
            };

            _projectRepository = new ProjectRepository(_dbContextProviderMock.Object)
            {
                MoProvider = _serviceProviderMock.Object
            };

            _taskRepository = new TaskRepository(_dbContextProviderMock.Object)
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
        public async Task InsertAsync_WithComplexEntity_ShouldInsertEntityWithNavigationProperties()
        {
            // Arrange
            var department = new Department
            {
                Name = "Engineering",
                Description = "Software Engineering Department",
                Metadata = new DepartmentMetadata
                {
                    Location = "Building A",
                    Floor = 3,
                    IsActive = true,
                }
            };

            // Act
            var result = await _departmentRepository.InsertAsync(department);
            await _dbContext.SaveChangesAsync();

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id, Is.Not.EqualTo(default(Guid)));

            var savedDepartment = await _dbContext.Departments.FindAsync(result.Id);
            Assert.That(savedDepartment, Is.Not.Null);
            Assert.That(savedDepartment.Name, Is.EqualTo("Engineering"));
            Assert.That(savedDepartment.Metadata.Location, Is.EqualTo("Building A"));
            Assert.That(savedDepartment.Metadata.Floor, Is.EqualTo(3));
        }

        [Test]
        public async Task InsertAsync_WithRelatedEntities_ShouldCreateEntityGraph()
        {
            // Arrange
            var department = new Department
            {
                Name = "Research & Development",
                Description = "R&D Department"
            };

            var employee = new Employee
            {
                FirstName = "John",
                LastName = "Doe",
                Email = "john.doe@example.com",
                Salary = 90000,
                ContactInfo = new ContactInformation
                {
                    PhoneNumber = "123-456-7890",
                    Address = "123 Main St",
                    City = "New York",
                    Country = "USA",
                    PostalCode = "10001"
                },
                Department = department
            };

            // Act - First insert the department to establish the relationship
            await _departmentRepository.InsertAsync(department);
            await _dbContext.SaveChangesAsync();

            // Then insert the employee with the department reference
            await _employeeRepository.InsertAsync(employee);
            await _dbContext.SaveChangesAsync();

            // Assert
            var savedEmployee = await _dbContext.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Email == "john.doe@example.com");

            Assert.That(savedEmployee, Is.Not.Null);
            Assert.That(savedEmployee.Department, Is.Not.Null);
            Assert.That(savedEmployee.Department.Name, Is.EqualTo("Research & Development"));
            Assert.That(savedEmployee.ContactInfo.PhoneNumber, Is.EqualTo("123-456-7890"));
            Assert.That(savedEmployee.ContactInfo.City, Is.EqualTo("New York"));
        }

        [Test]
        public async Task TrackGraph_ShouldProperlyTrackChangesToComplexGraph()
        {
            // Arrange - Create a complex entity graph
            var department = new Department
            {
                Name = "Product Development",
                Description = "Product Development Department"
            };

            var employee1 = new Employee
            {
                FirstName = "Alice",
                LastName = "Smith",
                Email = "alice.smith@example.com",
                Salary = 85000,
                Department = department,
                ContactInfo = new ContactInformation
                {
                    PhoneNumber = "123-456-7890",
                    Address = "123 Alice St",
                    City = "Dev City",
                    Country = "USA",
                    PostalCode = "12345"
                }
            };

            var employee2 = new Employee
            {
                FirstName = "Bob",
                LastName = "Johnson",
                Email = "bob.johnson@example.com",
                Salary = 78000,
                Department = department,
                ContactInfo = new ContactInformation
                {
                    PhoneNumber = "123-456-7891",
                    Address = "123 Bob St",
                    City = "Dev City",
                    Country = "USA",
                    PostalCode = "12345"
                }
            };

            var project = new Project
            {
                Name = "New Website",
                Description = "Company website redesign",
                StartDate = DateTime.Today,
                Budget = 50000,
                Status = ProjectStatus.Planning
            };

            // Add employees to project
            project.Employees.Add(employee1);
            project.Employees.Add(employee2);

            // Add tasks to project
            var task1 = new TaskEntity
            {
                Title = "Design Homepage",
                Description = "Create mockups for homepage",
                DueDate = DateTime.Today.AddDays(14),
                Priority = TaskPriority.High,
                Project = project,
                AssignedTo = employee1
            };

            var task2 = new TaskEntity
            {
                Title = "Setup Development Environment",
                Description = "Configure development server and tools",
                DueDate = DateTime.Today.AddDays(7),
                Priority = TaskPriority.Medium,
                Project = project,
                AssignedTo = employee2
            };

            project.Tasks.Add(task1);
            project.Tasks.Add(task2);

            // Save the initial graph
            await _departmentRepository.InsertAsync(department);
            await _employeeRepository.InsertManyAsync(new[] { employee1, employee2 });
            await _projectRepository.InsertAsync(project);
            await _dbContext.SaveChangesAsync();

            // Act - Modify the graph
            // 1. Update the project status
            project.Status = ProjectStatus.InProgress;
            project.Description = "Company website redesign - Updated";

            // 2. Change task assignment
            task1.AssignedTo = employee2;
            task1.Priority = TaskPriority.Critical;

            // 3. Change employee salary
            employee1.Salary = 90000;

            // 4. Update the department
            department.Metadata.Location = "Building B";

            // Use the repository to update with the changes
            await ((IMoRepository<Project>)_projectRepository).GetDbContextAsync();
            var dbContext = await ((IMoRepository)_projectRepository).GetDbContextAsync();

            // Track the entire graph from the project entry point
            dbContext.ChangeTracker.TrackGraph(project, node =>
            {
                var entry = node.Entry;
                var entity = entry.Entity;

                if (entity is Project p)
                {
                    entry.State = EntityState.Modified;
                }
                else if (entity is TaskEntity t)
                {
                    entry.State = EntityState.Modified;
                }
                else if (entity is Employee e)
                {
                    entry.State = EntityState.Modified;
                }
                else if (entity is Department d)
                {
                    entry.State = EntityState.Modified;
                }
            });

            await _dbContext.SaveChangesAsync();

            // Assert - Verify the changes were properly tracked and saved
            var updatedProject = await _dbContext.Projects
                .Include(p => p.Tasks)
                .ThenInclude(t => t.AssignedTo)
                .Include(p => p.Employees)
                .ThenInclude(e => e.Department)
                .FirstOrDefaultAsync(p => p.Id == project.Id);

            Assert.That(updatedProject, Is.Not.Null);
            Assert.That(updatedProject.Status, Is.EqualTo(ProjectStatus.InProgress));
            Assert.That(updatedProject.Description, Is.EqualTo("Company website redesign - Updated"));

            // Check task assignment
            var updatedTask1 = updatedProject.Tasks.FirstOrDefault(t => t.Id == task1.Id);
            Assert.That(updatedTask1, Is.Not.Null);
            Assert.That(updatedTask1.AssignedTo.Email, Is.EqualTo("bob.johnson@example.com"));
            Assert.That(updatedTask1.Priority, Is.EqualTo(TaskPriority.Critical));

            // Check employee salary via separate query to ensure it was updated
            var updatedEmployee1 = await _dbContext.Employees.FindAsync(employee1.Id);
            Assert.That(updatedEmployee1, Is.Not.Null);
            Assert.That(updatedEmployee1.Salary, Is.EqualTo(90000));

            // Check department location via separate query
            var updatedDepartment = await _dbContext.Departments.FindAsync(department.Id);
            Assert.That(updatedDepartment, Is.Not.Null);
            Assert.That(updatedDepartment.Metadata.Location, Is.EqualTo("Building B"));
        }

        [Test]
        public async Task UpdateAsync_WithNavigationProperties_ShouldUpdateRelatedEntities()
        {
            // Arrange - Create initial entity graph
            var department = new Department 
            { 
                Name = "Marketing",
                Description = "Marketing department"
            };
            var employee = new Employee
            {
                FirstName = "Jane",
                LastName = "Smith",
                Email = "jane.smith@example.com",
                Department = department,
                ContactInfo = new ContactInformation
                {
                    PhoneNumber = "123-456-7890",
                    Address = "123 Marketing St",
                    City = "Business City",
                    Country = "USA",
                    PostalCode = "12345"
                }
            };

            await _departmentRepository.InsertAsync(department);
            await _employeeRepository.InsertAsync(employee);
            await _dbContext.SaveChangesAsync();

            // Detach entities from context to simulate a fresh retrieval
            _dbContext.ChangeTracker.Clear();

            // Act - Load, modify, and update
            var savedEmployee = await _employeeRepository.GetAsync(
                e => e.Email == "jane.smith@example.com",
                includeDetails: true);

            savedEmployee.FirstName = "Janet";
            savedEmployee.Department.Name = "Digital Marketing";

            await _employeeRepository.UpdateAsync(savedEmployee);
            await _dbContext.SaveChangesAsync();

            // Assert
            _dbContext.ChangeTracker.Clear();

            var updatedEmployee = await _dbContext.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == employee.Id);

            Assert.That(updatedEmployee, Is.Not.Null);
            Assert.That(updatedEmployee.FirstName, Is.EqualTo("Janet"));

            // Verify the department name was also updated
            Assert.That(updatedEmployee.Department, Is.Not.Null);
            Assert.That(updatedEmployee.Department.Name, Is.EqualTo("Digital Marketing"));
        }

        [Test]
        public async Task DeleteAsync_WithCascade_ShouldDeleteRelatedEntities()
        {
            // Arrange - Create entity graph with relationships
            var department = new Department 
            { 
                Name = "Temporary Department",
                Description = "Temporary department for testing"
            };
            var employee1 = new Employee
            {
                FirstName = "Temp",
                LastName = "User1",
                Email = "temp1@example.com",
                Department = department,
                ContactInfo = new ContactInformation
                {
                    PhoneNumber = "123-456-7890",
                    Address = "123 Test St",
                    City = "Test City",
                    Country = "Test Country",
                    PostalCode = "12345"
                }
            };
            var employee2 = new Employee
            {
                FirstName = "Temp",
                LastName = "User2",
                Email = "temp2@example.com",
                Department = department,
                ContactInfo = new ContactInformation
                {
                    PhoneNumber = "123-456-7891",
                    Address = "124 Test St",
                    City = "Test City",
                    Country = "Test Country",
                    PostalCode = "12345"
                }
            };

            await _departmentRepository.InsertAsync(department);
            await _employeeRepository.InsertManyAsync([employee1, employee2]);
            await _dbContext.SaveChangesAsync();

            // Verify initial state
            var employeeCount = await _dbContext.Employees.CountAsync(e => e.DepartmentId == department.Id);
            Assert.That(employeeCount, Is.EqualTo(2));

            // Act - Delete the department (should cascade to employees)
            await _departmentRepository.DeleteAsync(department);
            await _dbContext.SaveChangesAsync();

            // Assert - Department and employees should be deleted
            var deletedDepartment = await _dbContext.Departments.FindAsync(department.Id);
            Assert.That(deletedDepartment, Is.Null);

            // Employees should be deleted due to cascade delete
            var remainingEmployees = await _dbContext.Employees.CountAsync(e => e.DepartmentId == department.Id);
            Assert.That(remainingEmployees, Is.EqualTo(0));
        }

        [Test]
        public async Task WithDetailsAsync_ShouldLoadNavigationProperties()
        {
            // Arrange - Create entity graph
            var department = new Department 
            { 
                Name = "Sales",
                Description = "Sales department"
            };
            var employee = new Employee
            {
                FirstName = "Mike",
                LastName = "Jones",
                Email = "mike.jones@example.com",
                Department = department,
                ContactInfo = new ContactInformation
                {
                    PhoneNumber = "123-456-7890",
                    Address = "123 Sales St",
                    City = "Sales City",
                    Country = "USA",
                    PostalCode = "12345"
                }
            };
            var project = new Project
            {
                Name = "Sales Campaign",
                Description = "Sales campaign project",
                StartDate = DateTime.Today,
                Status = ProjectStatus.Planning
            };
            project.Employees.Add(employee);

            await _departmentRepository.InsertAsync(department);
            await _employeeRepository.InsertAsync(employee);
            await _projectRepository.InsertAsync(project);
            await _dbContext.SaveChangesAsync();

            // Clear change tracker to simulate fresh retrieval
            _dbContext.ChangeTracker.Clear();

            // Act
            var queryable = await _projectRepository.WithDetailsAsync();
            var projectWithDetails = await queryable
                .Include(p => p.Employees)
                .ThenInclude(e => e.Department)
                .FirstOrDefaultAsync(p => p.Id == project.Id);

            // Assert
            Assert.That(projectWithDetails, Is.Not.Null);
            Assert.That(projectWithDetails.Employees, Is.Not.Null);
            Assert.That(projectWithDetails.Employees.Count, Is.EqualTo(1));

            var loadedEmployee = projectWithDetails.Employees.First();
            Assert.That(loadedEmployee.Email, Is.EqualTo("mike.jones@example.com"));

            // The Department should be loaded for each employee
            Assert.That(loadedEmployee.Department, Is.Not.Null);
            Assert.That(loadedEmployee.Department.Name, Is.EqualTo("Sales"));
        }
    }
}