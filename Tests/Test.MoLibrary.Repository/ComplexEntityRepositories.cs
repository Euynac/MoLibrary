using BuildingBlocksPlatform.Repository;
using BuildingBlocksPlatform.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Test.MoLibrary.Repository
{
    public class DepartmentRepository(IDbContextProvider<ComplexEntityDbContext> dbContextProvider)
        : MoRepository<ComplexEntityDbContext, Department, Guid>(dbContextProvider)
    {
        public override async Task<IQueryable<Department>> WithDetailsAsync()
        {
            var dbContext = await GetDbContextAsync();
            return dbContext.Departments
                .Include(d => d.Employees)
                .AsQueryable();
        }
    }
    
    public class EmployeeRepository(IDbContextProvider<ComplexEntityDbContext> dbContextProvider)
        : MoRepository<ComplexEntityDbContext, Employee, Guid>(dbContextProvider)
    {
        public override async Task<IQueryable<Employee>> WithDetailsAsync()
        {
            var dbContext = await GetDbContextAsync();
            return dbContext.Employees
                .Include(e => e.Department)
                .Include(e => e.Projects)
                .AsQueryable();
        }
    }
    
    public class ProjectRepository(IDbContextProvider<ComplexEntityDbContext> dbContextProvider)
        : MoRepository<ComplexEntityDbContext, Project, Guid>(dbContextProvider)
    {
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
    
    public class TaskRepository(IDbContextProvider<ComplexEntityDbContext> dbContextProvider)
        : MoRepository<ComplexEntityDbContext, TaskEntity, Guid>(dbContextProvider)
    {
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