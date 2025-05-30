using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Calculator.Tools;

[McpServerToolType]
public class McpResourceListHandler
{
    private readonly IServiceProvider _serviceProvider;

    public McpResourceListHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [McpServerTool, Description("Lists all available tools/resources on this server")]
    public object ListResources()
    {
        var resources = new List<object>();
        
        // Find all types with McpServerToolType attribute
        var toolTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null);
            
        foreach (var toolType in toolTypes)
        {
            var toolMethods = toolType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
                .Select(m => new 
                {
                    Name = $"{toolType.Name}.{m.Name}",
                    Description = m.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "",
                    Parameters = m.GetParameters().Select(p => new 
                    {
                        Name = p.Name,
                        Type = p.ParameterType.Name
                    }).ToArray()
                });
                
            resources.AddRange(toolMethods);
        }
        
        return new { Resources = resources };
    }
}