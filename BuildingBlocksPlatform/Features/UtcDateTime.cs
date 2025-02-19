using System.Globalization;
using BuildingBlocksPlatform.Converters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.Features;

public class AutoUtcDateTimeModelBinder : IModelBinder
{
    private readonly DateTimeModelBinder _dateTimeModelBinder;
    public AutoUtcDateTimeModelBinder(DateTimeModelBinder dateTimeModelBinder)
    {
     
        _dateTimeModelBinder = dateTimeModelBinder;
    }

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        await _dateTimeModelBinder.BindModelAsync(bindingContext);
        if (bindingContext.Result is {IsModelSet: true, Model: DateTime dateTime})
        {
            bindingContext.Result = ModelBindingResult.Success(JsonShared.NormalizeInTime(dateTime));
        }
    }
}

public class AutoUtcDateTimeModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var modelType = context.Metadata.UnderlyingOrModelType;
        if (modelType == typeof(DateTime))
        {
            const DateTimeStyles supportedStyles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal;
            var dateTimeModelBinder = new DateTimeModelBinder(supportedStyles, context.Services.GetRequiredService<ILoggerFactory>());
            return new AutoUtcDateTimeModelBinder(dateTimeModelBinder);
        }

        return null;
    }
   
}


/// <summary>
/// 自动将本地时间转为UTC时间的类型
/// </summary>
/// https://learn.microsoft.com/en-us/aspnet/core/mvc/models/model-binding?view=aspnetcore-8.0#bind-with-iparsablettryparse
[Obsolete("暂未完成，仅供参考")]
public class UtcDateTime : IParsable<UtcDateTime>
{
    public DateTime? DateTime { get; init; }
    public static UtcDateTime Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new FormatException($"无法将字符串{s}转换为UtcDateTime");
        }
        return result;
    }

    public static bool TryParse(string? s, IFormatProvider? provider, out UtcDateTime result)
    {
        if (string.IsNullOrEmpty(s))
        {
            result = new UtcDateTime();
            return true;
        }

        if (System.DateTime.TryParse(s, provider, DateTimeStyles.None, out var dateTime))
        {
            result = new UtcDateTime { DateTime = dateTime };
            return true;
        }

        result = new UtcDateTime();
        return false;
    }
}