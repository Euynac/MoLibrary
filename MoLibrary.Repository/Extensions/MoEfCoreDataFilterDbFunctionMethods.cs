using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace MoLibrary.Repository.Extensions;

public static class MoEfCoreDataFilterDbFunctionMethods
{
    public const string NotSupportedExceptionMessage = "Your EF Core database provider does not support 'User-defined function mapping'." +
                                                        "Please set 'UseDbFunction' of 'AbpEfCoreGlobalFilterOptions' to false to disable it." +
                                                        "See https://learn.microsoft.com/en-us/ef/core/querying/user-defined-function-mapping for more information.";

    public static bool SoftDeleteFilter(bool isDeleted, bool boolParam)
    {
        throw new NotSupportedException(NotSupportedExceptionMessage);
    }

    public static MethodInfo SoftDeleteFilterMethodInfo => typeof(MoEfCoreDataFilterDbFunctionMethods).GetMethod(nameof(SoftDeleteFilter))!;

    public static ModelBuilder ConfigureSoftDeleteDbFunction(this ModelBuilder modelBuilder, MethodInfo methodInfo)
    {
        modelBuilder.HasDbFunction(methodInfo)
            .HasTranslation(args =>
            {
                // (bool isDeleted, bool boolParam)
                var isDeleted = args[0];
                var boolParam = args[1];
                // IsDeleted == false
                return new SqlBinaryExpression(
                    ExpressionType.Equal,
                    isDeleted,
                    new SqlConstantExpression(Expression.Constant(false), boolParam.TypeMapping),
                    boolParam.Type,
                    boolParam.TypeMapping);


                //// empty where sql
                //return new SqlConstantExpression(Expression.Constant(true), boolParam.TypeMapping);
            });

        return modelBuilder;
    }
}
