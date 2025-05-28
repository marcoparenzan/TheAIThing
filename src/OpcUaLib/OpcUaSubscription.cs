using Opc.Ua;
using Opc.Ua.Client;

namespace OpcUaLib;

public class OpcUaSubscription
{
    private readonly Session session;
    private Action<NodeId, DataValue> handler;

    private Subscription subscription;

    public OpcUaSubscription(Session session, Action<NodeId, DataValue> handler, params string[] nodeIds)
    {
        this.session = session;
        this.handler = handler;

        this.subscription = new Subscription(session.DefaultSubscription)
        {
            PublishingInterval = 1000,
            KeepAliveCount = 10,
            LifetimeCount = 30,
            Priority = 100
        };

        session.AddSubscription(subscription);

        subscription.Create();

        foreach (var nodeId in nodeIds)
        {
            // Replace "ns=2;i=4" with your node ID
            var monitoredItem = new MonitoredItem(subscription.DefaultItem)
            {
                //DisplayName = "MyMonitoredItem",
                StartNodeId = new NodeId(nodeId),
                AttributeId = Attributes.Value,
                SamplingInterval = 1000
            };

            monitoredItem.Notification += (MonitoredItem item, MonitoredItemNotificationEventArgs e) =>
            {
                foreach (var value in item.DequeueValues())
                {
                    this.handler(item.ResolvedNodeId, value);
                }
            };

            subscription.AddItem(monitoredItem);
        }
        subscription.ApplyChanges();
    }

    internal void Delete()
    {
        subscription.Delete(true);
    }
}
