using BuildingBlocksPlatform.Repository;
using BuildingBlocksPlatform.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Test.MoLibrary.Repository
{
    public class DepartmentRepository : MoRepository<ComplexEntityDbContext, Department, Guid>
    {
        public DepartmentRepository(IDbContextProvider<ComplexEntityDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }
        
        public override async Task<IQueryable<Department>> WithDetailsAsync()
        {
            var dbContext = await GetDbContextAsync();
            return dbContext.Departments
                .Include(d => d.Employees)
                .AsQueryable();
        }
    }
    
    public class EmployeeRepository : MoRepository<ComplexEntityDbContext, Employee, Guid>
    {
        public EmployeeRepository(IDbContextProvider<ComplexEntityDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }
        
        public override async Task<IQueryable<Employee>> WithDetailsAsync()
        {
            var dbContext = await GetDbContextAsync();
            return dbContext.Employees
                .Include(e => e.Department)
                .Include(e => e.Projects)
                .AsQueryable();
        }
    }
    
    public class ProjectRepository : MoRepository<ComplexEntityDbContext, Project, Guid>
    {
        public ProjectRepository(IDbContextProvider<ComplexEntityDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }
        
        public override async Task<IQueryable<Project>> WithDetailsAsync()
        {
            var dbContext = await GetDbContextAsync();
            return dbContext.Projects
                .Include(p => p.Employees)
                .Include(p => p.Tasks)
                    .ThenInclude(t => t.AssignedTo)
                .AsQueryable();
        }
    }
    
    public class TaskRepository : MoRepository<ComplexEntityDbContext, TaskEntity, Guid>
    {
        public TaskRepository(IDbContextProvider<ComplexEntityDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }
        
        public override async Task<IQueryable<TaskEntity>> WithDetailsAsync()
        {
            var dbContext = await GetDbContextAsync();
            return dbContext.Tasks
                .Include(t => t.Project)
                .Include(t => t.AssignedTo)
                    .ThenInclude(e => e.Department)
                .AsQueryable();
        }
    }
} 