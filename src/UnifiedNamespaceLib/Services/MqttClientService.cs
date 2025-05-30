using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Packets;
using System.Text.Json;

namespace UnifiedNamespaceLib.Services;

public class MqttClientService([ServiceKey] string serviceKey, IConfiguration config, IServiceProvider sp, ILogger<MqttClientService> logger) : IMessagingService
{
    MqttClientFactory mqttFactory;
    IMqttClient mqttClient;
    string clientId;

    List<Action<MqttApplicationMessage>> handlers = new();
    List<MqttTopicFilter> topicFilters;

    public async Task<MqttClientService> ConnectAsync()
    {
        var stay = true;
        while (stay)
        {
            try
            {
                await ConnectImplAsync();
                stay = false;
            }
            catch (Exception ex)
            {
                logger.LogWarning($"MqttClient {clientId} failed...then retry");
            }
        }
        return this;
    }

    private async Task<MqttClientService> ConnectImplAsync()
    {
        var serviceConfig = config.GetSection(serviceKey);
        var credentialsName = serviceConfig["credentialsName"];
        var credentialConfig = config.GetSection(credentialsName);
        if (string.IsNullOrWhiteSpace(credentialsName)) credentialConfig = serviceConfig.GetSection("credentials");

        EnsureMqttClient();

        var username = credentialConfig["username"];
        var password = credentialConfig["password"];
        this.clientId = (serviceConfig["clientId"] ?? serviceKey) ?? username;
        var host = credentialConfig["host"];
        var port = 1883;
        int.TryParse(credentialConfig["port"], out port);

        var mqttClientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId(clientId)
            .WithCredentials(username, password)
            .WithTimeout(TimeSpan.FromSeconds(60))
            .WithWillRetain(true)    
        ;

        //var pemKey = X509Certificate2.CreateFromPemFile(
        //    serviceConfig["certificate:pem"],
        //    serviceConfig["certificate:key"]
        //);
        //var pkcs12 = pemKey.Export(X509ContentType.Pkcs12);
        //var certificate = new X509Certificate2(pkcs12);

        //mqttClientOptionsBuilder = mqttClientOptionsBuilder
        //    .WithTlsOptions(configure =>
        //        configure
        //            .UseTls()
        //            .WithClientCertificates([certificate])
        //);
        var mqttClientOptions = mqttClientOptionsBuilder.Build();


        mqttClient.DisconnectedAsync += async (s) =>
        {
            await mqttClient.ReconnectAsync();
        };

        mqttClient.ApplicationMessageReceivedAsync += (s) =>
        {
            foreach (var handler in handlers)
            {
                try
                {
                    handler(s.ApplicationMessage);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"MqttClient {clientId}: {ex.Message}");
                }
            }
            return Task.CompletedTask;
        };


        var connectResponse = await mqttClient.ConnectAsync(mqttClientOptions, CancellationToken.None);

        return this;
    }

    private void EnsureMqttClient()
    {
        if (mqttClient is null)
        {
            this.mqttFactory = new MqttClientFactory();
            this.mqttClient = mqttFactory.CreateMqttClient();
        }
    }

    async Task<MqttClientService> DisconnectAsync()
    {
        // Send a clean disconnect to the server by calling _DisconnectAsync_. Without this the TCP connection
        // gets dropped and the server will handle this as a non clean disconnect (see MQTT spec for details).
        var mqttClientDisconnectOptions = mqttFactory.CreateClientDisconnectOptionsBuilder().Build();

        await mqttClient.DisconnectAsync(mqttClientDisconnectOptions, CancellationToken.None);

        return this;
    }

    public async Task<MqttClientService> PublishAsync<TPayload>(string topic, TPayload payload, bool retainFlag = true)
    {
        if (mqttClient is null) throw new InvalidOperationException("mqttClient is null");
        if (!mqttClient.IsConnected) throw new InvalidOperationException("!mqttClient.IsConnected");

        try
        {
            var applicationMessage = new MqttApplicationMessageBuilder()
                        .WithTopic(topic)
                        .WithPayload(JsonSerializer.Serialize(payload))
                        //.WithRetainFlag(retainFlag)
                        .Build();

            var result = await mqttClient.PublishAsync(applicationMessage, CancellationToken.None);
        }
        catch (Exception ex)
        {
            throw ex;
        }

        return this;
    }

    public MqttClientService Handle(Action<MqttApplicationMessage> handler)
    {
        handlers.Add(handler);
        return this;
    }

    public async Task<MqttClientService> SubscribeAsync(params string[] topics)
    {
        EnsureMqttClient();
            
        if (topicFilters is not null)
        {
            await mqttClient.UnsubscribeAsync(new MqttClientUnsubscribeOptions
            {
                TopicFilters = topicFilters.Select(xx => xx.Topic).ToList()
            });
        }

        this.topicFilters = topics.Select(xx => new MqttTopicFilter
        {
            Topic = xx
            //, RetainHandling = MQTTnet.Protocol.MqttRetainHandling.SendAtSubscribe
        }).ToList();
        var subOpts = new MqttClientSubscribeOptions
        {
            TopicFilters = topicFilters
        };
        var result = await mqttClient.SubscribeAsync(subOpts);

        return this;
    }
}
