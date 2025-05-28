using ToolsLib;

namespace KernelPlayground;

public class Functions(HttpHandler httpHandler)
{
    public string[] ElencoDelleStanze() =>
        ["livingroom", "kitchen", "bedroom"];

    public string[] LuciAcceseNellaStanza(string room) =>
        room switch
        {
            "living room" => new[] { "lampadaA", "lampadarioE" },
            "kitchner" => new[] { "lampadaB", "lampadarioF" },
            "bedroom" => new[] { "lampadaC", "lampadarioG" },
            _ => Array.Empty<string>()
        };

    public async Task<object> Riassumi(string diQualcosa)
    {
        return diQualcosa;
    }

    public async Task<object> RunCode(string language, string code)
    {
        return null;
    }

    public async Task<object> TagsOPCUA(string[] tags, string server)
    {
        return null;
    }

    public async Task<object> Schedule(string what)
    {
        return null;
    }

    public async Task<object> CercaInHttp(string what)
    {
        return httpHandler.GetAsync(what);
    }
}
