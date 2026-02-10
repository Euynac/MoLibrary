namespace Monica.AI.RAG.Models;

/// <summary>
/// Options for configuring RAG search behavior.
/// Inspired by agent-framework's TextSearchProviderOptions.
/// </summary>
public class RAGSearchOptions
{
    public RAGSearchBehavior SearchBehavior { get; set; } =
        RAGSearchBehavior.OnDemandFunctionCalling;

    public string FunctionToolName { get; set; } = "Search";

    public string FunctionToolDescription { get; set; } =
        "Allows searching for additional information to help answer the user question.";

    public string ContextPrompt { get; set; } =
        "## Additional Context\nConsider the following information from source documents when responding to the user:";

    public string CitationsPrompt { get; set; } =
        "Include citations to the source document with document name and link if available.";

    public Func<IList<TextSearchResult>, string>? ContextFormatter { get; set; }

    public int RecentMessageMemoryLimit { get; set; }

    public int DefaultTopK { get; set; } = 5;
}

public enum RAGSearchBehavior
{
    /// <summary>
    /// Execute search prior to each AI invocation and inject results as a message.
    /// Maps to AIContextProvider returning AIContext.Messages.
    /// </summary>
    BeforeAIInvoke,

    /// <summary>
    /// Expose a function tool for the LLM to invoke on-demand.
    /// Maps to AIContextProvider returning AIContext.Tools.
    /// </summary>
    OnDemandFunctionCalling
}
