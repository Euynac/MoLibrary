using BuildingBlocksPlatform.Repository.EntityInterfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Test.MoLibrary.Repository
{
    // Base entity for all complex test entities
    public class ComplexEntityBase : IMoEntity<Guid>
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = "TestUser";
        
        public virtual object[] GetKeys()
        {
            return new object[] { Id };
        }
        
        public virtual void AutoSetNewId(bool notSetWhenNotDefault = false)
        {
            if (notSetWhenNotDefault && Id != default)
                return;
                
            Id = Guid.NewGuid();
        }
        
        public virtual void SetNewId(Guid key)
        {
            Id = key;
        }
    }
    
    // Department with a collection of employees (one-to-many)
    public class Department : ComplexEntityBase
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DepartmentMetadata Metadata { get; set; } = new DepartmentMetadata();
        
        // Navigation property - one-to-many relationship
        public virtual ICollection<Employee> Employees { get; set; } = new Collection<Employee>();
    }
    
    // Nested class for department metadata
    public class DepartmentMetadata
    {
        public string Location { get; set; } = "Main Office";
        public int Floor { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public Dictionary<string, string> ExtraInformation { get; set; } = new Dictionary<string, string>();
    }
    
    // Employee entity with navigation properties
    public class Employee : ComplexEntityBase
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public decimal Salary { get; set; }
        public ContactInformation ContactInfo { get; set; } = new ContactInformation();
        
        // Navigation property - many-to-one relationship
        public Guid DepartmentId { get; set; }
        public virtual Department Department { get; set; }
        
        // Navigation property - many-to-many relationship
        public virtual ICollection<Project> Projects { get; set; } = new Collection<Project>();
    }
    
    // Nested class for employee contact information
    public class ContactInformation
    {
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }
    }
    
    // Project entity for many-to-many relationship with employees
    public class Project : ComplexEntityBase
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal Budget { get; set; }
        public ProjectStatus Status { get; set; } = ProjectStatus.Planning;
        
        // Navigation property - many-to-many relationship
        public virtual ICollection<Employee> Employees { get; set; } = new Collection<Employee>();
        
        // Navigation property - one-to-many relationship
        public virtual ICollection<Task> Tasks { get; set; } = new Collection<Task>();
    }
    
    // Task entity that belongs to a project
    public class Task : ComplexEntityBase
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime? DueDate { get; set; }
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public TaskStatus Status { get; set; } = TaskStatus.NotStarted;
        
        // Navigation property - many-to-one relationship
        public Guid ProjectId { get; set; }
        public virtual Project Project { get; set; }
        
        // Navigation property - many-to-one relationship (optional)
        public Guid? AssignedToId { get; set; }
        public virtual Employee AssignedTo { get; set; }
    }
    
    // Enums for complex entities
    public enum ProjectStatus
    {
        Planning,
        InProgress,
        OnHold,
        Completed,
        Cancelled
    }
    
    public enum TaskPriority
    {
        Low,
        Medium,
        High,
        Critical
    }
    
    public enum TaskStatus
    {
        NotStarted,
        InProgress,
        InReview,
        Completed
    }
} 