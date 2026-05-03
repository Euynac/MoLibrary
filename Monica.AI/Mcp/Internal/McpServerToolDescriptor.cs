using System.Reflection;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using Monica.AI.Mcp.Models;
using MonicaMcpServer = Monica.AI.Mcp.Abstractions.McpServer;

namespace Monica.AI.Mcp.Internal;

internal sealed record McpServerToolDescriptor(
    string ServerName,
    string? SkillName,
    McpServerTransportKind TransportKind,
    bool IsLocalToolEnabled,
    string Name,
    string Description,
    MethodInfo Method,
    object Target,
    McpServerTool SdkTool,
    AITool? AgentTool);
