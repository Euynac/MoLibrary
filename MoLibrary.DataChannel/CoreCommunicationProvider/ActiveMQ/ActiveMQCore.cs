using Apache.NMS;
using Apache.NMS.ActiveMQ.Commands;
using BuildingBlocksPlatform.DataChannel.CoreCommunication;
using BuildingBlocksPlatform.DataChannel.Pipeline;
using Microsoft.Extensions.Logging;

namespace BuildingBlocksPlatform.DataChannel.CoreCommunicationProvider.ActiveMQ;

public class ActiveMQCore(MetadataForActiveMQ metadata, ILogger<ActiveMQCore> logger) : CommunicationCore<MetadataForActiveMQ>(metadata)
{
    private ISession? session;
    private IMessageProducer producer;

    public override async Task ReceiveDataAsync(DataContext data)
    {
        if (session != null && producer != null)
        {
            ITextMessage msg = await session.CreateTextMessageAsync(data.Data?.ToString());
            msg.Properties.SetString("Type", data.DataType.ToString()); //设置消息种类
            await producer.SendAsync(msg);
        }
    }

    public override async Task InitAsync()
    {
        var brokerUri = Metadata.BrokerUri;
        var factory = new NMSConnectionFactory(brokerUri); 
        var connection = await factory.CreateConnectionAsync(Metadata.AccessKey, Metadata.SecretKey);
        connection.ClientId = Metadata.ClientId;
        await connection.StartAsync();
        session = await connection.CreateSessionAsync(AcknowledgementMode.AutoAcknowledge);//自动签收

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
                        //如果是ClientAcknowledge或者IndividualAcknowledge，需要调用Acknowledge方法进行签收确认
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
                        //如果是ClientAcknowledge或者IndividualAcknowledge，需要调用Acknowledge方法进行签收确认
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
                producer.DeliveryMode = MsgDeliveryMode.NonPersistent; //消息发送模式：持久化或非持久化
            }
        }
    }

    public override EConnectionDirection SupportedConnectionDirection()
    {
        return EConnectionDirection.InputAndOutput;
    }
} 