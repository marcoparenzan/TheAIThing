using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Schema.Binary;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Serialization;

namespace OpcUaLib;

public  class OpcUaClient(string serviceKey, string endpointUrl, string username, string password)
{
    ApplicationConfiguration cfg;
    Session session;

    List<OpcUaSubscription> subscriptions = new();

    public async Task ConnectAsync()
    {
        this.cfg = new ApplicationConfiguration()
        {
            ApplicationName = "OPC UA Console Client",
            ApplicationUri = $"urn:{System.Net.Dns.GetHostName()}:{serviceKey}",
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                AutoAcceptUntrustedCertificates = true,
                ApplicationCertificate = new CertificateIdentifier(),
                TrustedPeerCertificates = new CertificateTrustList()
                {
                    StorePath = "certs\\peer"
                },
                TrustedIssuerCertificates = new CertificateTrustList()
                {
                    StorePath = "certs\\issuers"
                },
                RejectedCertificateStore = new CertificateStoreIdentifier()
            },
            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 }
        };

        await cfg.Validate(ApplicationType.Client);

        // Create the certificate if it doesn't exist
        if (File.Exists("cert.pfx"))
        {
            var bytes = File.ReadAllBytes("cert.pfx");
            var cert = X509CertificateLoader.LoadPkcs12(bytes, "password");
            cfg.SecurityConfiguration.ApplicationCertificate.Certificate = cert;
        }
        else
        {
            Console.WriteLine("Creating self-signed certificate...");

            // Create certificate using CertificateFactory
            var cert = CertificateFactory.CreateCertificate(
                cfg.SecurityConfiguration.ApplicationCertificate.StoreType,
                cfg.SecurityConfiguration.ApplicationCertificate.StorePath,
                null,
                cfg.ApplicationUri, // depends on servicekey
                cfg.ApplicationName,
                "CN=OPC UA Console Client",
                new string[] { System.Net.Dns.GetHostName() },
                2048,
                DateTime.UtcNow - TimeSpan.FromDays(1),
                12,
                256,  // SHA-1 (use 1 for SHA-256 if available)
                false
            );
            var bytes = cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx, "password");
            File.WriteAllBytes("cert.pfx", bytes);

            cfg.SecurityConfiguration.ApplicationCertificate.Certificate = cert;
        }

        Console.WriteLine($"Connecting to: {endpointUrl}");

        // Create the endpoint description
        var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointUrl, false);
        Console.WriteLine($"Selected endpoint: {selectedEndpoint.EndpointUrl}");

        // Create the session
        var endpointConfiguration = EndpointConfiguration.Create(cfg);
        var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

        // Connect to the server
        this.session = await Session.Create(
            cfg,
            endpoint,
            false,
            cfg.ApplicationName,
            60000,
            new UserIdentity(username, password),
            null
        );
    }

    public async Task CloseAsync()
    {
        foreach (var sub in subscriptions)
        {
            sub.Delete();
        }
        subscriptions.Clear();
        session.Close();
        session.Dispose();
        session = null;
    }

    public async Task<OpcUaSubscription> SubscribeAsync(Action<NodeId, DataValue> handler, params string[] nodeIds)
    {
        var newSub = new OpcUaSubscription(session, handler, nodeIds);

        subscriptions.Add(newSub);

        return newSub;
    }

    public async Task<Node> ReadNodeAsync(NodeId nodeId)
    {
        return await this.session.ReadNodeAsync(nodeId);
    }

    public async Task<DataValue> ReadValueAsync(NodeId nodeId)
    {
        return await this.session.ReadValueAsync(nodeId);
    }

    public async Task<(Node node, object value)> ParseAsync(NodeId nodeId)
    {
        var aaa = await session.ReadValueAsync(nodeId);
        return await ParseAsync(nodeId, aaa);
    }

    public async Task<(Node node, object value)> ParseAsync(NodeId nodeId, DataValue aaa)
    {
        var node = await session.ReadNodeAsync(nodeId);
        if (node is VariableNode vnode)
        {
            if (aaa.WrappedValue.TypeInfo.BuiltInType == BuiltInType.ExtensionObject)
            {
                var instance = await ParseComplexTypeAsync(vnode, aaa);
                return (node, instance);
            }
            // Handle UInt32 as a special case
            return (node, aaa.WrappedValue.Value);
        }
        else
        {
            return (node, aaa.Value);
        }
    }

    async Task<Dictionary<string, object>> ParseComplexTypeAsync(VariableNode vnode, DataValue aaa)
    {
        var byteArrayBody = (byte[])((ExtensionObject)aaa.Value).Body;
        var decoder = new BinaryDecoder((byte[])byteArrayBody, null);

        var dataType = await session.ReadNodeAsync(vnode.DataType);

        var jsonEncodingId = new NodeId(vnode.JsonEncodingId.Identifier, vnode.NodeId.NamespaceIndex);
        var jsonEncoding = await session.ReadNodeAsync(jsonEncodingId);
        var jsonEncodedValue = await session.ReadValueAsync(jsonEncodingId);
        var jsonEncodedBytes = (byte[])jsonEncodedValue.Value;
        var jsonEncodedStream = new MemoryStream(jsonEncodedBytes);

        var typeDictionarySerializer = new XmlSerializer(typeof(Opc.Ua.Schema.Binary.TypeDictionary));
        var typeDictionaryDeserialized = (Opc.Ua.Schema.Binary.TypeDictionary)typeDictionarySerializer.Deserialize(jsonEncodedStream);

        var instance = new Dictionary<string, object>();
        var rootType = (StructuredType)typeDictionaryDeserialized.Items.SingleOrDefault(xx => xx.Name == dataType.DisplayName);
        StructuredType(typeDictionaryDeserialized, decoder, instance, rootType);

        return instance;
    }

    void StructuredType(TypeDictionary typeDictionary, BinaryDecoder reader, Dictionary<string, object> item, StructuredType? root)
    {
        foreach (var field in root.Field)
        {
            if (field.TypeName.Namespace == "http://opcfoundation.org/BinarySchema/")
            {
                switch (field.TypeName.Name)
                {
                    case "Int32":
                        var int32Value = reader.ReadInt32(field.Name);
                        item.Add(field.Name, int32Value);
                        break;
                    default:
                        throw new NotSupportedException($"{field.TypeName.Name} not found - http://opcfoundation.org/BinarySchema/");
                }
            }
            else
            {
                var child = typeDictionary.Items.SingleOrDefault(xx => xx.Name == field.TypeName.Name);
                if (child is StructuredType subType)
                {
                    var subItem = new Dictionary<string, object>();
                    StructuredType(typeDictionary, reader, subItem, subType);
                    item.Add(field.Name, subItem);
                }
                else if (child is EnumeratedType et)
                {
                    var value = EnumeratedType(typeDictionary, reader, et);
                    item.Add(field.Name, value);
                }
            }
        }
    }

    string EnumeratedType(TypeDictionary typeDictionary, BinaryDecoder reader, EnumeratedType? root)
    {
        var vvv = reader.ReadInt32(root.Name);
        var v = root.EnumeratedValue.SingleOrDefault(xx => xx.Value == vvv);
        return v.Name;
    }
}
