using Apache.NMS;
using Apache.NMS.ActiveMQ.Commands;
using Microsoft.Extensions.Logging;
using Monica.DataChannel.CoreCommunication;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.CoreCommunicationProvider.ActiveMQ;

public class ActiveMQCore(MetadataForActiveMQ metadata, ILogger<ActiveMQCore> logger) : CommunicationCore<MetadataForActiveMQ>(metadata)
{
    private ISession? session;
    private IMessageProducer? producer;

    public override async Task ReceiveDataAsync(DataContext data)
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

        if (Metadata.Direction == EConnectionDirection.Input || Metadata.Direction == EConnectionDirection.InputAndOutput)
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

                        logger.LogInformation("ActivateMQ接收到消息：{message}", message);
                        // If using ClientAcknowledge or IndividualAcknowledge, call Acknowledge() here.
                        //message?.Acknowledge();
                    }
                    catch (Exception e)
                    {
                        logger.LogError("ActivateMQ接收消息出现异常。{Exception}", e);
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

                        logger.LogInformation("ActivateMQ接收到消息：{message}", message);
                        // If using ClientAcknowledge or IndividualAcknowledge, call Acknowledge() here.
                        //message?.Acknowledge();
                    }
                    catch (Exception e)
                    {
                        logger.LogError("ActivateMQ接收消息出现异常。{Exception}", e);
                        throw;
                    }
                };
            }
        }

        if (Metadata.Direction == EConnectionDirection.Output || Metadata.Direction == EConnectionDirection.InputAndOutput)
        {
            if (!string.IsNullOrEmpty(Metadata.QueueName))
            {
                var dest = await session.GetQueueAsync(Metadata.QueueName);
                producer = session.CreateProducer(dest);
                producer.DeliveryMode = MsgDeliveryMode.NonPersistent; // Delivery mode (persistent vs non-persistent).
            }
        }
    }

    public override EConnectionDirection SupportedConnectionDirection()
    {
        return EConnectionDirection.InputAndOutput;
    }
}
