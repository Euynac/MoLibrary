using System.Collections.Generic;

namespace MoLibrary.Generators.AutoController.Models;

/// <summary>
/// A record to store all the relevant info for a candidate handler.
/// </summary>
internal class HandlerCandidate(
    string classRoute,
    List<string> tags,
    string handlerType,
    string methodName,
    string httpMethodAttribute,
    string httpMethodRoute,
    string requestType,
    string responseType,
    string documentationComment,
    IEnumerable<string> originalUsings,
    string candidateNamespace,
    bool hasFromFormAttribute = false)
{
    /// <summary>
    /// The route path associated with the handler class.
    /// </summary>
    public string ClassRoute { get; } = classRoute;

    /// <summary>
    /// The list of tags associated with the handler for grouping and documentation.
    /// </summary>
    public List<string> Tags { get; } = tags;

    /// <summary>
    /// The fully qualified type name of the handler class.
    /// </summary>
    public string HandlerType { get; } = handlerType;

    /// <summary>
    /// The name of the handler method that will be called.
    /// </summary>
    public string MethodName { get; } = methodName;

    /// <summary>
    /// The HTTP method attribute (e.g., HttpGet, HttpPost) applied to the handler.
    /// </summary>
    public string HttpMethodAttribute { get; } = httpMethodAttribute;

    /// <summary>
    /// The route template specified in the HTTP method attribute.
    /// </summary>
    public string HttpMethodRoute { get; } = httpMethodRoute;

    /// <summary>
    /// The fully qualified type name of the request/input parameter type.
    /// </summary>
    public string RequestType { get; } = requestType;

    /// <summary>
    /// The fully qualified type name of the response/return type.
    /// </summary>
    public string ResponseType { get; } = responseType;

    /// <summary>
    /// The XML documentation comment associated with the handler method.
    /// </summary>
    public string DocumentationComment { get; } = documentationComment;

    /// <summary>
    /// The collection of using statements from the original handler class.
    /// </summary>
    public IEnumerable<string> OriginalUsings { get; } = originalUsings;

    /// <summary>
    /// The namespace where the candidate handler is defined.
    /// </summary>
    public string CandidateNamespace { get; } = candidateNamespace;

    /// <summary>
    /// Indicates whether the handler method has a FromForm attribute for form data binding.
    /// </summary>
    public bool HasFromFormAttribute { get; } = hasFromFormAttribute;
}