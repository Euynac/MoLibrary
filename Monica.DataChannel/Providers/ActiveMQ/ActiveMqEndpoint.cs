using Apache.NMS;
using Apache.NMS.ActiveMQ.Commands;
using Microsoft.Extensions.Logging;
using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Providers.ActiveMQ;

public class ActiveMqEndpoint(ActiveMqOptions metadata, ILogger<ActiveMqEndpoint> logger) : CommunicationEndpointBase<ActiveMqOptions>(metadata)
{
    private ISession? session;
    private IMessageProducer? producer;

    public override async Task ReceiveDataAsync(ChannelDataContext data)
    {
        if (session is null || producer is null)
        {
            return;
        }

        ITextMessage msg = await session.CreateTextMessageAsync(data.Data?.ToString());
        msg.Properties.SetString("Type", data.DataType?.Name); // Sets the message type.
        await producer.SendAsync(msg);
    }

    public override async Task InitAsync(CancellationToken cancellationToken = default)
    {
        var brokerUri = Metadata.BrokerUri;
        var factory = new NMSConnectionFactory(brokerUri); 
        var connection = await factory.CreateConnectionAsync(Metadata.AccessKey, Metadata.SecretKey);
        connection.ClientId = Metadata.ClientId;
        await connection.StartAsync();
        session = await connection.CreateSessionAsync(AcknowledgementMode.AutoAcknowledge);// Auto-acknowledge mode.

        if (Metadata.Direction == ConnectionDirection.Input || Metadata.Direction == ConnectionDirection.InputAndOutput)
        {
            if (!string.IsNullOrEmpty(Metadata.QueueName))
            {
                var destinationForQueue = await session.GetQueueAsync(Metadata.QueueName);
                var consumerForQueue = await session.CreateConsumerAsync(destinationForQueue, null, false);
                consumerForQueue.Listener += message =>
                {
                    try
                    {
                        if (message is ActiveMQTextMessage text)
                        {
                            SendData(text.Text);
                        }

                        logger.LogInformation("ActiveMQ received a message: {Message}", message);
                        // If using ClientAcknowledge or IndividualAcknowledge, call Acknowledge() here.
                        //message?.Acknowledge();
                    }
                    catch (Exception e)
                    {
                        logger.LogError("ActiveMQ failed to receive a message. Error: {Exception}", e);
                        throw;
                    }
                };
            }

            if (!string.IsNullOrEmpty(Metadata.TopicName))
            {
                var destination = await session.GetTopicAsync(Metadata.TopicName);
                var consumer =
                    await session.CreateDurableConsumerAsync(destination, Metadata.SubscriptionName, null, false);
                consumer.Listener += message =>
                {
                    try
                    {
                        if (message is ActiveMQTextMessage text)
                        {
                            SendData(text.Text);
                        }

                        logger.LogInformation("ActiveMQ received a message: {Message}", message);
                        // If using ClientAcknowledge or IndividualAcknowledge, call Acknowledge() here.
                        //message?.Acknowledge();
                    }
                    catch (Exception e)
                    {
                        logger.LogError("ActiveMQ failed to receive a message. Error: {Exception}", e);
                        throw;
                    }
                };
            }
        }

        if (Metadata.Direction == ConnectionDirection.Output || Metadata.Direction == ConnectionDirection.InputAndOutput)
        {
            if (!string.IsNullOrEmpty(Metadata.QueueName))
            {
                var dest = await session.GetQueueAsync(Metadata.QueueName);
                producer = session.CreateProducer(dest);
                producer.DeliveryMode = MsgDeliveryMode.NonPersistent; // Delivery mode (persistent vs non-persistent).
            }
        }
    }

    public override ConnectionDirection SupportedConnectionDirection()
    {
        return ConnectionDirection.InputAndOutput;
    }
}
