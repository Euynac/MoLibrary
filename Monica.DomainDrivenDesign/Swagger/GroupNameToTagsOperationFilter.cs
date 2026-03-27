namespace Monica.DomainDrivenDesign.Swagger;
//
// /// <summary>
// /// Operation filter that maps ApiExplorer.GroupName to Swagger tags.
// /// TODO: Revisit this when evaluating the .NET 10 API changes.
// /// </summary>
// internal class GroupNameToTagsOperationFilter : IOperationFilter
// {
//     public void Apply(OpenApiOperation operation, OperationFilterContext context)
//     {
//         var apiDescription = context.ApiDescription;
//         
//         if (!string.IsNullOrEmpty(apiDescription.GroupName))
//         {
//             operation.Tags ??= new List<OpenApiTag>();
//             operation.Tags.Clear(); // By default, Swagger adds a tag that matches the controller name unless a tag is assigned explicitly.
//             var controllerName = context.MethodInfo.DeclaringType?.Name ?? "Unknown";
//             operation.Tags.Add(new OpenApiTag
//             {
//                 Name = apiDescription.GroupName,
//                 Description = $"{controllerName} related endpoints"
//             });
//         }
//     }
// } 
