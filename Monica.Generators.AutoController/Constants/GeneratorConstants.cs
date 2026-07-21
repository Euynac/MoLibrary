namespace Monica.Generators.AutoController.Constants;

internal static class GeneratorConstants
{
    public const string API_ENDPOINT_ATTRIBUTE = "Monica.WebApi.Annotations.ApiEndpointAttribute";
    public const string WEB_API_CONFIG_ATTRIBUTE = "Monica.WebApi.Annotations.WebApiGenerationConfigAttribute";
    public const string APPLICATION_SERVICE_NAMESPACE = "Monica.WebApi.Abstractions";
    public const string GENERATED_CONTROLLER_NAMESPACE = "GeneratedControllers";

    public const int BINDING_AUTO = 0;
    public const int BINDING_QUERY = 1;
    public const int BINDING_BODY = 2;
    public const int BINDING_FORM = 3;

    public const int METHOD_GET = 0;
    public const int METHOD_POST = 1;
    public const int METHOD_PUT = 2;
    public const int METHOD_PATCH = 3;
    public const int METHOD_DELETE = 4;
}
