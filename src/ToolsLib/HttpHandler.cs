using System.Threading.Channels;

namespace ToolsLib;

public class HttpHandler
{
    private Task requestHandlerTask;
    private HttpClient httpClient;
    private Channel<string> channelIn;
    private Channel<string> channelOut;

    public HttpHandler()
    {
        this.httpClient = new HttpClient();
        this.channelIn = Channel.CreateUnbounded<string>();
        this.channelOut = Channel.CreateUnbounded<string>();
        this.requestHandlerTask = Task.Factory.StartNew(this.HandleRequestsAsync);
    }

    async Task HandleRequestsAsync()
    {
        while (true)
        {
            var request = await channelIn.Reader.ReadAsync();

            var result = await httpClient.GetAsync(request);
            var content = await result.Content.ReadAsStringAsync();

            await channelOut.Writer.WriteAsync(content);
        }
    }

    public async Task<object> CercaInHttpAsync(string url)
    {
        await channelIn.Writer.WriteAsync(url);
        var content = await channelOut.Reader.ReadAsync();
        string text = FilterHtml(content);

        return text;
    }

    private string FilterHtml(string content)
    {
        var htmlDoc = new HtmlAgilityPack.HtmlDocument();
        htmlDoc.LoadHtml(content);
        var text = htmlDoc.DocumentNode.InnerText;
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    public async Task<object> GetAsync(string uri)
    {
        await channelIn.Writer.WriteAsync(uri);
        var content = await channelOut.Reader.ReadAsync();
        return content;
    }

    public async Task<Stream> OpenAsync(string uri)
    {
        return await httpClient.GetStreamAsync(uri);
    }
}
