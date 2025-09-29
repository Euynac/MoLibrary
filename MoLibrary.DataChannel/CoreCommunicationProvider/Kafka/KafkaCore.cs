using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using MoLibrary.DataChannel.CoreCommunication;
using MoLibrary.DataChannel.Pipeline;

namespace MoLibrary.DataChannel.CoreCommunicationProvider.Kafka;

public class KafkaCore(MetadataForKafka metadata, ILogger<KafkaCore> logger) : CommunicationCore<MetadataForKafka>(metadata)
{
    private IProducer<string, string>? _producer;
    private IConsumer<string, string>? _consumer;
    private CancellationTokenSource? _consumerCts;
    private Task? _consumerTask;

    public override async Task ReceiveDataAsync(DataContext data)
    {
        if (_producer != null && !string.IsNullOrEmpty(Metadata.Topic))
        {
            try
            {
                var message = new Message<string, string>
                {
                    Key = data.DataType?.Name ?? "default",
                    Value = data.Data?.ToString() ?? string.Empty,
                    Headers = new Headers
                    {
                        { "Type", System.Text.Encoding.UTF8.GetBytes(data.DataType?.Name ?? "unknown") }
                    }
                };

                var deliveryReport = await _producer.ProduceAsync(Metadata.Topic, message);
                logger.LogInformation("Kafka message sent to topic {Topic}, partition {Partition}, offset {Offset}", 
                    deliveryReport.Topic, deliveryReport.Partition, deliveryReport.Offset);
            }
            catch (ProduceException<string, string> ex)
            {
                CollectException(ex, this, $"Failed to send message to Kafka: {ex.Error.Reason}", logger);
            }
        }
    }

    public override async Task InitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Initialize producer for output direction
            if (Metadata.Direction == EConnectionDirection.Output || Metadata.Direction == EConnectionDirection.InputAndOutput)
            {
                var producerConfig = new ProducerConfig
                {
                    BootstrapServers = Metadata.BootstrapServers,
                    ClientId = Metadata.ClientId,
                    SecurityProtocol = Metadata.SecurityProtocol
                };

                if (!string.IsNullOrEmpty(Metadata.Username) && !string.IsNullOrEmpty(Metadata.Password))
                {
                    producerConfig.SaslMechanism = SaslMechanism.Plain;
                    producerConfig.SaslUsername = Metadata.Username;
                    producerConfig.SaslPassword = Metadata.Password;
                }

                _producer = new ProducerBuilder<string, string>(producerConfig).Build();
                logger.LogInformation("Kafka producer initialized for topic: {Topic}", Metadata.Topic);
            }

            // Initialize consumer for input direction
            if (Metadata.Direction == EConnectionDirection.Input || Metadata.Direction == EConnectionDirection.InputAndOutput)
            {
                var consumerConfig = new ConsumerConfig
                {
                    BootstrapServers = Metadata.BootstrapServers,
                    GroupId = Metadata.ConsumerGroupId,
                    ClientId = Metadata.ClientId,
                    AutoOffsetReset = Metadata.AutoOffsetReset,
                    EnableAutoCommit = Metadata.EnableAutoCommit,
                    SecurityProtocol = Metadata.SecurityProtocol
                };

                if (Metadata.EnableAutoCommit)
                {
                    consumerConfig.AutoCommitIntervalMs = Metadata.AutoCommitIntervalMs;
                }

                if (!string.IsNullOrEmpty(Metadata.Username) && !string.IsNullOrEmpty(Metadata.Password))
                {
                    consumerConfig.SaslMechanism = Metadata.SaslMechanism;
                    consumerConfig.SaslUsername = Metadata.Username;
                    consumerConfig.SaslPassword = Metadata.Password;
                }

                _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
                
                if (!string.IsNullOrEmpty(Metadata.Topic))
                {
                    _consumer.Subscribe(Metadata.Topic);
                    
                    _consumerCts = new CancellationTokenSource();
                    _consumerTask = Task.Run(async () => await ConsumeMessages(_consumerCts.Token), _consumerCts.Token);
                    
                    logger.LogInformation("Kafka consumer initialized and subscribed to topic: {Topic}", Metadata.Topic);
                }
            }
        }
        catch (Exception ex)
        {
            CollectException(ex, this, "Kafka initialization failed", logger);
            throw;
        }
    }

    private async Task ConsumeMessages(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _consumer != null)
            {
                try
                {
                    var consumeResult = _consumer.Consume(cancellationToken);
                    if (consumeResult != null && !string.IsNullOrEmpty(consumeResult.Message?.Value))
                    {
                        logger.LogInformation("Kafka message received from topic {Topic}, partition {Partition}, offset {Offset}",
                            consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);

                        SendData(consumeResult.Message.Value);

                        // Manual commit if auto-commit is disabled
                        if (!Metadata.EnableAutoCommit)
                        {
                            _consumer.Commit(consumeResult);
                        }
                    }
                }
                catch (ConsumeException ex)
                {
                    CollectException(ex, this, $"Failed to consume message from Kafka: {ex.Error.Reason}", logger);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            CollectException(ex, this, "Kafka consumer loop failed", logger);
        }
    }

    public override async Task DisposeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Cancel consumer loop
            _consumerCts?.Cancel();
            
            // Wait for consumer task to complete
            if (_consumerTask != null)
            {
                await _consumerTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }

            // Close consumer
            _consumer?.Close();
            _consumer?.Dispose();

            // Flush and dispose producer
            if (_producer != null)
            {
                _producer.Flush(TimeSpan.FromSeconds(10));
                _producer.Dispose();
            }

            _consumerCts?.Dispose();
            
            logger.LogInformation("Kafka core disposed successfully");
        }
        catch (Exception ex)
        {
            CollectException(ex, this, "Kafka disposal failed", logger);
        }
    }

    public override EConnectionDirection SupportedConnectionDirection()
    {
        return EConnectionDirection.InputAndOutput;
    }
}