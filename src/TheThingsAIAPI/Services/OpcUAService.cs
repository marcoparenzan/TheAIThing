using MQTTnet;
using Opc.Ua;
using OpcUaLib;
using Org.BouncyCastle.Crypto;
using System.Text.Json;
using UnifiedNamespaceLib.Services;

namespace TheAIThingAPI.Services;

public class OpcUAService([ServiceKey] string serviceKey, IConfiguration config, IServiceProvider sp, ILogger<OpcUaClient> logger) : IWorkerService
{
    private IMessagingService? messagingService;
    private List<NodeId> nodes;

    public async Task ExecuteAsync()
    {
        var workerConfig = config.GetSection("workers").GetSection(serviceKey);
        var cts = new CancellationTokenSource();

        var opcUaClient = new OpcUaClient(serviceKey, workerConfig[$"endpointUrl"], workerConfig[$"username"], workerConfig[$"password"]);

        try
        {
            await opcUaClient.ConnectAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing OPC UA client: {ex.Message}");
            return;
        }


        try
        {
            this.nodes = new List<NodeId>((workerConfig[$"nodes"] ?? "").Split('|').Select(xx => new NodeId(xx)));
            this.messagingService = sp.GetKeyedService<IMessagingService>(workerConfig[$"messagingClient"]);
            messagingService.Handle(async msg =>
            {
                if (msg.Topic.StartsWith("timers/"))
                {
                    var i = 0;
                    while (true)
                    {
                        if (i >= nodes.Count) break;
                        try
                        {
                            while(true)
                            {
                                if (i >= nodes.Count) break;
                                var node = nodes[i];

                                var (_, value) = await opcUaClient.ParseAsync(node);
                                await PublishAsync($"devices/{serviceKey}/nodes/{node}", value);

                                i++;
                            }
                        }
                        catch(Exception ex)
                        {
                            Console.WriteLine($"Error reading node {nodes[i]}: {ex.Message}");
                        }
                    }
                }
            });
            await messagingService.ConnectAsync();
            await messagingService.SubscribeAsync("#");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing MQTT client: {ex.Message}");
            return;
        }

        await Task.Delay(-1);
    }

    async Task PublishAsync<TMessage>(string topic, TMessage message)
    {
        Console.WriteLine($"{topic}: {JsonSerializer.Serialize(message)}");
        await messagingService.PublishAsync(topic, message, retainFlag: false);
    }
}
