using ToolsLib;

namespace KernelWithContext;

public class Functions(HttpHandler httpHandler, string url)
{
    public async Task<object> ExecuteAsync(string what)
    {
        return await httpHandler.CercaInHttpAsync($"{url}/{what}");
    }
}
