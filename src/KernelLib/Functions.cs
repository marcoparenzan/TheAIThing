using ToolsLib;

namespace KernelLib;

public class Functions(HttpHandler httpHandler)
{
    ShellyProxy bedroomProxy = new("http://shelly1g4-7c2c676ea328");
    Dictionary<string, bool> statoCondizionamento = new();

    public string[] StanzeDiUnaCasa() =>
        ["living room", "kitchen", "bedroom"];

    public string[] LuciAcceseNellaStanza(string room) =>
        room switch
        {
            "livingroom" => new[] { "lampadaA", "lampadarioE" },
            "kitchen" => new[] { "lampadaB", "lampadarioF" },
            "bedroom" => new[] { "lampadaC", "lampadarioG" },
            _ => Array.Empty<string>()
        };

    public bool CondizionatoreAcceso(string stanza)
    {
        if (stanza == "bedroom")
        {
            return bedroomProxy.Stato();
        }
        if (!statoCondizionamento.ContainsKey(stanza))
        {
            statoCondizionamento[stanza] = false; // Assume the door is open by default
        }
        return statoCondizionamento[stanza];
    }

    public bool ToggleCondizionamento(string room)
    {
        if (room == "bedroom")
        {
            var xxx = bedroomProxy.Toggle();
            return xxx;
        }

        var stato = CondizionatoreAcceso(room);
        statoCondizionamento[room] = !stato; // Toggle the state
        return !stato; // Return the new state
    }

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

    public async Task<object> CercaInHttp(string url)
    {
        var result = await httpHandler.CercaInHttpAsync(url);
        return result;
    }

    public async Task<object> InviaViaEMail(string cosa, string email)
    {
        return default;
    }
}
