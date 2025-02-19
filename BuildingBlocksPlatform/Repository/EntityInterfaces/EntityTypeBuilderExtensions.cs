using BuildingBlocksPlatform.Repository.EFCoreExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BuildingBlocksPlatform.Repository.EntityInterfaces;

public static class EntityTypeBuilderExtensions
{
    public static void ConfigureByConvention(this EntityTypeBuilder b)
    {
        b.TryConfigureConcurrencyStamp();
        b.TryConfigureExtraProperties();
        //b.TryConfigureHasCreator();
        //b.TryConfigureSoftDelete();
        //b.TryConfigureDeletionTime();
        //b.TryConfigureDeletionAudited();
        //b.TryConfigureCreationTime();
        //b.TryConfigureLastModificationTime();
    }

    public static void TryConfigureConcurrencyStamp(this EntityTypeBuilder b)
    {
        if (b.Metadata.ClrType.IsAssignableTo<IHasConcurrencyStamp>())
        {
            b.Property(nameof(IHasConcurrencyStamp.ConcurrencyStamp))
                .IsConcurrencyToken()
                .HasMaxLength(MoRepositoryOptions.ConcurrencyStampMaxLength)
                .HasColumnName(nameof(IHasConcurrencyStamp.ConcurrencyStamp));
        }
    }

    public static void TryConfigureExtraProperties(this EntityTypeBuilder b)
    {
        if (!b.Metadata.ClrType.IsAssignableTo<IHasExtraProperties>())
        {
            return;
        }

        b.Property<ExtraPropertyDictionary>(nameof(IHasExtraProperties.ExtraProperties))
            .HasColumnName(nameof(IHasExtraProperties.ExtraProperties))
            .HasConversion(new ExtraPropertiesValueConverter(b.Metadata.ClrType))
            .Metadata.SetValueComparer(new ExtraPropertyDictionaryValueComparer());
    }


    //public static void TryConfigureSoftDelete(this EntityTypeBuilder b)
    //{
    //    if (b.Metadata.ClrType.IsAssignableTo<IHasSoftDelete>())
    //    {
    //        b.Property(nameof(IHasSoftDelete.IsDeleted))
    //            .IsRequired()
    //            .HasDefaultValue(false)
    //            .HasColumnName(nameof(IHasSoftDelete.IsDeleted));
    //    }
    //}

    //public static void TryConfigureDeletionTime(this EntityTypeBuilder b)
    //{
    //    if (b.Metadata.ClrType.IsAssignableTo<IHasDeletionTime>())
    //    {
    //        b.TryConfigureSoftDelete();

    //        b.Property(nameof(IHasDeletionTime.DeletionTime))
    //            .IsRequired(false)
    //            .HasColumnName(nameof(IHasDeletionTime.DeletionTime));
    //    }
    //}

    //public static void TryConfigureHasCreator(this EntityTypeBuilder b)
    //{
    //    if (b.Metadata.ClrType.IsAssignableTo<IHasCreator>())
    //    {
    //        b.Property(nameof(IHasCreator.CreatorId))
    //            .IsRequired(false)
    //            .HasColumnName(nameof(IHasCreator.CreatorId));
    //    }
    //}
    

    //public static void TryConfigureDeletionAudited(this EntityTypeBuilder b)
    //{
    //    if (b.Metadata.ClrType.IsAssignableTo<IHasDeleter>())
    //    {
    //        b.TryConfigureDeletionTime();

    //        b.Property(nameof(IHasDeleter.DeleterId))
    //            .IsRequired(false)
    //            .HasColumnName(nameof(IHasDeleter.DeleterId));
    //    }
    //}

    //public static void TryConfigureCreationTime(this EntityTypeBuilder b)
    //{
    //    if (b.Metadata.ClrType.IsAssignableTo<IHasCreationTime>())
    //    {
    //        b.Property(nameof(IHasCreationTime.CreationTime))
    //            .IsRequired()
    //            .HasColumnName(nameof(IHasCreationTime.CreationTime));
    //    }
    //}

    //public static void TryConfigureLastModificationTime(this EntityTypeBuilder b)
    //{
    //    if (b.Metadata.ClrType.IsAssignableTo<IHasModificationTime>())
    //    {
    //        b.Property(nameof(IHasModificationTime.LastModificationTime))
    //            .IsRequired(false)
    //            .HasColumnName(nameof(IHasModificationTime.LastModificationTime));
    //    }
    //}
}
