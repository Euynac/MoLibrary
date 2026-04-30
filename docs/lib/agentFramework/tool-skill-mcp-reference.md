# Microsoft Agent Framework: AITool, Skills, and MCP

> **Status.** Source-grounded reference note.
> **Audience.** Monica AI and framework developers.
> **Source baseline.** `microsoft/agent-framework` .NET tag `dotnet-1.3.0`, plus official OpenAI MCP tool documentation checked on 2026-04-30.

## 1. Mental Model

Agent Framework exposes model capabilities through a small set of layers:

```text
AITool
  Low-level callable function/tool surface passed to the model.

Agent Skill
  Higher-level package of instructions, resources, and scripts.
  Internally exposed through prompt instructions plus AITools.

MCP client usage
  Remote or stdio MCP server tools are discovered and converted to AITools,
  or passed to the model provider as a hosted MCP server descriptor.

MCP server exposure
  This application exposes tools over MCP for other clients or model runtimes.
  The server does not assemble prompts or call the LLM unless a tool does so.
```

Use the lowest layer that matches the job:

| Need | Prefer |
|---|---|
| One direct operation, such as `get_weather` | `AITool` / `AIFunction` |
| A domain workflow with instructions, reference content, and callable scripts | Agent Skill |
| Reuse tools hosted by another process or service | MCP client or hosted MCP tool |
| Let other agents call this app's capabilities | MCP server |

## 2. `AITool`

`AITool` is the low-level model-facing tool abstraction from `Microsoft.Extensions.AI`. In normal use, Agent Framework receives a collection of `AITool` instances in `ChatOptions.Tools` and serializes them to the provider's tool format.

For a function tool, the model sees a JSON schema describing:

- tool name
- description
- parameters schema

Example C#:

```csharp
using Microsoft.Extensions.AI;

static string GetWeather(string location, string unit = "celsius")
{
    return $"Weather for {location}: 21 degrees {unit}.";
}

AITool weatherTool = AIFunctionFactory.Create(
    (string location, string unit) => GetWeather(location, unit),
    name: "get_weather",
    description: "Get the current weather for a location.");

AIAgent agent = chatClient.AsAIAgent(
    instructions: "Answer using tools when needed.",
    tools: [weatherTool]);
```

Conceptual OpenAI Chat Completions request:

```json
{
  "model": "gpt-5.4-mini",
  "messages": [
    {
      "role": "system",
      "content": "Answer using tools when needed."
    },
    {
      "role": "user",
      "content": "What is the weather in San Francisco?"
    }
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "get_weather",
        "description": "Get the current weather for a location.",
        "parameters": {
          "type": "object",
          "properties": {
            "location": {
              "type": "string"
            },
            "unit": {
              "type": "string"
            }
          },
          "required": ["location", "unit"]
        }
      }
    }
  ]
}
```

If the model chooses the tool, the model response contains a tool call similar to:

```json
{
  "name": "get_weather",
  "arguments": {
    "location": "San Francisco",
    "unit": "celsius"
  }
}
```

The application executes the delegate and sends the tool result back to the model. `AITool` is therefore the direct function-calling layer.

## 3. Agent Skills

Agent Framework Skills are implemented by `AgentSkillsProvider`, which is an `AIContextProvider`. The provider follows a progressive-disclosure pattern:

1. **Advertise**: inject skill names and descriptions into the model instructions.
2. **Load**: expose a `load_skill` tool that returns the full skill body.
3. **Read resources**: expose `read_skill_resource` when a skill has resources.
4. **Run scripts**: expose `run_skill_script` when a skill has scripts.

The important point: a skill does **not** replace `AITool`. A skill is a higher-level package that is exposed through generated `AIFunction` tools:

```text
AgentSkill
  -> AgentSkillsProvider
    -> AIContext.Instructions
    -> AIContext.Tools
      -> load_skill / read_skill_resource / run_skill_script
      -> AIFunction
      -> AITool
```

`AIContextProvider` merges provided context with the existing request context by concatenating instructions and appending tools. `ChatClientAgent` then writes the merged instructions and tools back into `ChatOptions` before calling the underlying model.

### 3.1 File-Based Skill

File-based skills live under a directory containing `SKILL.md`, optional `references/`, optional `assets/`, and optional `scripts/`.

Example `skills/unit-converter/SKILL.md`:

```markdown
---
name: unit-converter
description: Convert between common units using a multiplication factor.
---

## Usage

When the user requests a unit conversion:

1. Review `references/conversion-table.md` to find the factor.
2. Run `scripts/convert.py` with `--value <number> --factor <factor>`.
3. Present the converted value with both units.
```

Example registration:

```csharp
var skillsProvider = new AgentSkillsProvider(
    Path.Combine(AppContext.BaseDirectory, "skills"),
    SubprocessScriptRunner.RunAsync);

AIAgent agent = responsesClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = "UnitConverterAgent",
    ChatOptions = new()
    {
        Instructions = "You are a helpful assistant that can convert units."
    },
    AIContextProviders = [skillsProvider]
}, model: deploymentName);
```

Initial model-facing request conceptually contains:

```json
{
  "instructions": "You are a helpful assistant that can convert units.\n\nYou have access to skills containing domain-specific knowledge and capabilities.\nEach skill provides specialized instructions, reference documents, and assets for specific tasks.\n\n<available_skills>\n  <skill>\n    <name>unit-converter</name>\n    <description>Convert between common units using a multiplication factor.</description>\n  </skill>\n</available_skills>\n\nWhen a task aligns with a skill's domain, follow these steps in exact order:\n- Use `load_skill` to retrieve the skill's instructions.\n- Follow the provided guidance.\n- Use `read_skill_resource` to read any referenced resources, using the name exactly as listed.\n- Use `run_skill_script` to run referenced scripts, using the name exactly as listed.\nOnly load what is needed, when it is needed.",
  "input": "How many kilometers is 26.2 miles?",
  "tools": [
    {
      "type": "function",
      "name": "load_skill",
      "description": "Loads the full content of a specific skill",
      "parameters": {
        "type": "object",
        "properties": {
          "skillName": {
            "type": "string"
          }
        },
        "required": ["skillName"]
      }
    },
    {
      "type": "function",
      "name": "read_skill_resource",
      "description": "Reads a resource associated with a skill, such as references, assets, or dynamic data.",
      "parameters": {
        "type": "object",
        "properties": {
          "skillName": {
            "type": "string"
          },
          "resourceName": {
            "type": "string"
          }
        },
        "required": ["skillName", "resourceName"]
      }
    },
    {
      "type": "function",
      "name": "run_skill_script",
      "description": "Runs a script associated with a skill.",
      "parameters": {
        "type": "object",
        "properties": {
          "skillName": {
            "type": "string"
          },
          "scriptName": {
            "type": "string"
          },
          "arguments": {
            "type": "object"
          }
        },
        "required": ["skillName", "scriptName"]
      }
    }
  ]
}
```

Typical tool-call sequence:

```json
{
  "name": "load_skill",
  "arguments": {
    "skillName": "unit-converter"
  }
}
```

```json
{
  "name": "read_skill_resource",
  "arguments": {
    "skillName": "unit-converter",
    "resourceName": "references/conversion-table.md"
  }
}
```

```json
{
  "name": "run_skill_script",
  "arguments": {
    "skillName": "unit-converter",
    "scriptName": "scripts/convert.py",
    "arguments": {
      "value": 26.2,
      "factor": 1.60934
    }
  }
}
```

### 3.2 Code-Defined Inline Skill

Use `AgentInlineSkill` when the skill is simple and should be declared fluently in code.

```csharp
var unitConverterSkill = new AgentInlineSkill(
    name: "unit-converter",
    description: "Convert between miles, kilometers, pounds, and kilograms.",
    instructions: """
        Use this skill when the user asks to convert between units.

        1. Review the conversion-table resource.
        2. Use the convert script with the requested value and factor.
        """)
    .AddResource(
        "conversion-table",
        """
        | From      | To         | Factor  |
        |-----------|------------|---------|
        | miles     | kilometers | 1.60934 |
        | kilograms | pounds     | 2.20462 |
        """)
    .AddScript("convert", (double value, double factor) =>
    {
        double result = Math.Round(value * factor, 4);
        return JsonSerializer.Serialize(new { value, factor, result });
    });

var skillsProvider = new AgentSkillsProvider(unitConverterSkill);
```

This has the same runtime behavior as the file-based skill: the initial prompt advertises only `unit-converter`, and full content is returned only through `load_skill`.

### 3.3 Class-Based Skill

Use `AgentClassSkill<TSelf>` when the skill deserves a reusable class. This is the closest match to Monica's preferred class-based capability authoring.

```csharp
internal sealed class UnitConverterSkill : AgentClassSkill<UnitConverterSkill>
{
    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        "unit-converter",
        "Convert between miles, kilometers, pounds, and kilograms.");

    protected override string Instructions => """
        Use this skill when the user asks to convert between units.

        1. Read the conversion-table resource.
        2. Use the convert script with value and factor.
        3. Return the converted value with units.
        """;

    [AgentSkillResource("conversion-table")]
    [Description("Lookup table of multiplication factors.")]
    public string ConversionTable => """
        | From      | To         | Factor  |
        |-----------|------------|---------|
        | miles     | kilometers | 1.60934 |
        | kilograms | pounds     | 2.20462 |
        """;

    [AgentSkillScript("convert")]
    [Description("Multiplies a value by a factor.")]
    private static string Convert(double value, double factor)
    {
        double result = Math.Round(value * factor, 4);
        return JsonSerializer.Serialize(new { value, factor, result });
    }
}

var skillsProvider = new AgentSkillsProvider(new UnitConverterSkill());
```

When `load_skill` is called, Agent Framework synthesizes XML-like skill content:

```xml
<name>unit-converter</name>
<description>Convert between miles, kilometers, pounds, and kilograms.</description>

<instructions>
Use this skill when the user asks to convert between units.

1. Read the conversion-table resource.
2. Use the convert script with value and factor.
3. Return the converted value with units.
</instructions>

<resources>
  <resource name="conversion-table" description="Lookup table of multiplication factors."/>
</resources>

<scripts>
  <script name="convert" description="Multiplies a value by a factor.">
    <parameters_schema>{...}</parameters_schema>
  </script>
</scripts>
```

The model then calls:

```json
{
  "name": "read_skill_resource",
  "arguments": {
    "skillName": "unit-converter",
    "resourceName": "conversion-table"
  }
}
```

and:

```json
{
  "name": "run_skill_script",
  "arguments": {
    "skillName": "unit-converter",
    "scriptName": "convert",
    "arguments": {
      "value": 26.2,
      "factor": 1.60934
    }
  }
}
```

### 3.4 Skill vs `AITool`

Skills and `AITool` are not substitutes at the same layer:

| Aspect | `AITool` | Agent Skill |
|---|---|---|
| Layer | Low-level callable function | Higher-level capability package |
| Initial model context | Tool schema and description | Skill catalog plus generic skill tools |
| Model call | Calls concrete tool name directly | Calls `load_skill`, `read_skill_resource`, `run_skill_script` |
| Best for | Simple operations | Domain workflows with docs, assets, resources, scripts |
| Token behavior | Tool schema is present when registered | Full skill body is lazy-loaded |

Use a direct `AITool` for a single operation. Use a Skill when the operation needs reusable instructions, resources, and supporting scripts.

## 4. MCP Consumption

MCP can be used in two ways from an Agent Framework agent.

### 4.1 Local MCP Client Tools

In this mode, the application connects to an MCP server, lists its tools, casts the returned `McpClientTool` values to `AITool`, and passes those tools to the agent. Agent Framework or the MCP client executes the MCP call locally.

```csharp
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

await using McpClient mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new()
{
    Endpoint = new Uri("https://learn.microsoft.com/api/mcp"),
    Name = "Microsoft Learn MCP"
}));

IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync();
List<AITool> agentTools = [.. mcpTools.Cast<AITool>()];

AIAgent agent = aiProjectClient.AsAIAgent(
    deploymentName,
    instructions: "Use Microsoft Learn MCP tools for documentation questions.",
    name: "DocsAgent",
    tools: agentTools);
```

Model-facing request shape:

```json
{
  "model": "gpt-5.4-mini",
  "messages": [
    {
      "role": "system",
      "content": "Use Microsoft Learn MCP tools for documentation questions."
    },
    {
      "role": "user",
      "content": "How does one create an Azure storage account using az cli?"
    }
  ],
  "tools": [
    {
      "type": "function",
      "function": {
        "name": "microsoft_docs_search",
        "description": "Search Microsoft Learn documentation.",
        "parameters": {
          "type": "object",
          "properties": {
            "query": {
              "type": "string"
            }
          },
          "required": ["query"]
        }
      }
    }
  ]
}
```

The model sees a normal function tool. The MCP transport is hidden behind that `AITool`.

### 4.2 Hosted MCP Tool

In this mode, the model provider receives the MCP server URL. The provider runtime lists and calls MCP tools. Agent Framework does not execute the MCP call itself.

```csharp
using OpenAI.Responses;

var mcpTool = new HostedMcpServerTool(
    serverName: "microsoft_learn",
    serverAddress: "https://learn.microsoft.com/api/mcp")
{
    AllowedTools = ["microsoft_docs_search"],
    ApprovalMode = HostedMcpServerToolApprovalMode.NeverRequire
};

AIAgent agent = responsesClient.AsAIAgent(
    model: deploymentName,
    instructions: "Answer questions by searching Microsoft Learn only.",
    name: "MicrosoftLearnAgent",
    tools: [mcpTool]);
```

OpenAI Responses API request shape:

```json
{
  "model": "gpt-5.4-mini",
  "instructions": "Answer questions by searching Microsoft Learn only.",
  "input": "Summarize the Azure AI Agent documentation related to MCP Tool calling.",
  "tools": [
    {
      "type": "mcp",
      "server_label": "microsoft_learn",
      "server_url": "https://learn.microsoft.com/api/mcp",
      "allowed_tools": ["microsoft_docs_search"],
      "require_approval": "never"
    }
  ]
}
```

Typical response items:

```json
{
  "type": "mcp_list_tools",
  "server_label": "microsoft_learn",
  "tools": [
    {
      "name": "microsoft_docs_search",
      "description": "Search Microsoft Learn documentation.",
      "input_schema": {
        "type": "object",
        "properties": {
          "query": {
            "type": "string"
          }
        },
        "required": ["query"]
      }
    }
  ]
}
```

```json
{
  "type": "mcp_call",
  "server_label": "microsoft_learn",
  "name": "microsoft_docs_search",
  "arguments": "{\"query\":\"Azure AI Agent MCP Tool calling\"}",
  "output": "...search results...",
  "error": null
}
```

Use hosted MCP when the provider supports remote MCP natively and the server is trusted. Use local MCP client tools when the application needs direct control over transport, lifecycle, or authentication.

## 5. Providing an MCP Server

If the application only needs to expose an MCP server, there is no LLM request and no prompt assembly in the server. The server only handles MCP JSON-RPC lifecycle and tool calls:

```text
MCP client
  -> initialize
  -> tools/list
  -> tools/call
  -> server executes application code
  -> server returns MCP content
```

For local developer tools, stdio transport is common. For external consumers, use HTTP transport.

### 5.1 Expose a Plain Tool Over HTTP

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true;
    })
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapMcp("/mcp");
app.Run("http://0.0.0.0:3001");

[McpServerToolType]
public static class OrderTools
{
    [McpServerTool]
    [Description("Gets the status of an order by order id.")]
    public static string GetOrderStatus(string orderId)
    {
        return $"Order {orderId} is shipped.";
    }
}
```

External MCP endpoint:

```text
https://api.example.com/mcp
```

MCP `initialize` request:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {
    "protocolVersion": "2025-06-18",
    "capabilities": {},
    "clientInfo": {
      "name": "example-client",
      "version": "1.0.0"
    }
  }
}
```

MCP `tools/list` request:

```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/list"
}
```

MCP `tools/list` response:

```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "tools": [
      {
        "name": "GetOrderStatus",
        "description": "Gets the status of an order by order id.",
        "inputSchema": {
          "type": "object",
          "properties": {
            "orderId": {
              "type": "string"
            }
          },
          "required": ["orderId"]
        }
      }
    ]
  }
}
```

MCP `tools/call` request:

```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "GetOrderStatus",
    "arguments": {
      "orderId": "A10086"
    }
  }
}
```

MCP `tools/call` response:

```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Order A10086 is shipped."
      }
    ],
    "isError": false
  }
}
```

### 5.2 Expose an Agent as an MCP Tool

Agent Framework can convert an `AIAgent` to an `AIFunction`, then expose that function as an MCP server tool.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

AIAgent agent = aiProjectClient.AsAIAgent(agentVersion);

McpServerTool tool = McpServerTool.Create(agent.AsAIFunction());

HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools([tool]);

await builder.Build().RunAsync();
```

This is useful when the exposed tool should itself run an agent. Otherwise, prefer plain MCP tools that directly call application services.

## 6. Comparison

| Category | Who discovers tools? | Who executes the operation? | Model-facing request |
|---|---|---|---|
| Direct `AITool` | Application declares tools | Application executes delegate | `tools: [{ type: "function", ... }]` |
| Agent Skill | `AgentSkillsProvider` advertises skills and generic skill tools | Application executes `load_skill`, resource reads, and scripts | Prompt skill catalog plus `load_skill` / `read_skill_resource` / `run_skill_script` |
| Local MCP client tools | Application lists MCP server tools | Application/MCP client calls MCP server | Normal function tools converted from MCP tools |
| Hosted MCP tool | Model provider lists MCP server tools | Model provider calls MCP server | `tools: [{ type: "mcp", server_url, ... }]` |
| MCP server only | External MCP client lists server tools | This application executes tool methods | No LLM request inside the server |

## 7. Practical Guidance

- Use `AITool` for small, stable, direct functions.
- Use class-based `AgentClassSkill<TSelf>` for Monica-style domain capabilities with instructions and multiple scripts.
- Keep skill descriptions short because they are injected into the initial instructions.
- Put expensive or verbose reference material into skill resources so it is loaded only on demand.
- Use hosted MCP only for trusted remote servers. Remote MCP servers can see tool-call arguments sent by the model.
- Prefer HTTP transport for externally exposed MCP servers and stdio for local developer integration.
- If an MCP tool wraps an agent, be explicit about the agent's instructions because the MCP client will only see the tool name, description, and schema.

## 8. Source Pointers

Key Agent Framework source files in the cached third-party catalog:

- `dotnet/src/Microsoft.Agents.AI/Skills/AgentSkillsProvider.cs`
- `dotnet/src/Microsoft.Agents.AI.Abstractions/AIContextProvider.cs`
- `dotnet/src/Microsoft.Agents.AI/ChatClient/ChatClientAgent.cs`
- `dotnet/src/Microsoft.Agents.AI/Skills/Programmatic/AgentInlineSkill.cs`
- `dotnet/src/Microsoft.Agents.AI/Skills/Programmatic/AgentClassSkill.cs`
- `dotnet/samples/02-agents/AgentSkills/Agent_Step02_CodeDefinedSkills/Program.cs`
- `dotnet/samples/02-agents/AgentSkills/Agent_Step03_ClassBasedSkills/Program.cs`
- `dotnet/samples/02-agents/ModelContextProtocol/Agent_MCP_Server/Program.cs`
- `dotnet/samples/02-agents/ModelContextProtocol/ResponseAgent_Hosted_MCP/Program.cs`
- `dotnet/samples/02-agents/Agents/Agent_Step07_AsMcpTool/Program.cs`

External references:

- OpenAI Responses API MCP tool guide: `https://developers.openai.com/api/docs/guides/tools-connectors-mcp`
- MCP C# SDK getting started and server transport examples: `https://csharp.sdk.modelcontextprotocol.io/`

