//using Microsoft.Extensions.DependencyInjection;
//using System.Text.Json.Serialization.Metadata;




//namespace BuildingBlocksPlatform.Converters;

//public class NullToEmptyJsonConverterModifier
//{
//    private IServiceProvider _serviceProvider = default!;

//    public Action<JsonTypeInfo> CreateModifyAction(IServiceProvider serviceProvider)
//    {
//        _serviceProvider = serviceProvider;
//        return Modify;
//    }

//    private void Modify(JsonTypeInfo jsonTypeInfo)
//    {
//        foreach (var property in jsonTypeInfo.Properties.Where(x => x.PropertyType == typeof(DateTime) || x.PropertyType == typeof(DateTime?)))
//        {
//            if (property.AttributeProvider == null ||
//                !property.AttributeProvider.GetCustomAttributes(typeof(DisableDateTimeNormalizationAttribute), false).Any())
//            {
//                property.CustomConverter = property.PropertyType == typeof(DateTime)
//                    ? _serviceProvider.GetRequiredService<AbpDateTimeConverter>()
//                    : _serviceProvider.GetRequiredService<AbpNullableDateTimeConverter>();
//            }
//        }
//    }
//}