using Monica.Framework.Seeder.Abstractions;

namespace Monica.Framework.Seeder.Annotations;

/// <summary>
/// Declares that a seeder can run only after <typeparamref name="TSeeder"/> succeeds.
/// </summary>
/// <typeparam name="TSeeder">The prerequisite seeder discovered in the same host.</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class SeederDependsOnAttribute<TSeeder> : Attribute
    where TSeeder : ISeeder
{
}
