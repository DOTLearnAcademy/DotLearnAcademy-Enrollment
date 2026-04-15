using Amazon.SQS;
using Amazon.SQS.Model;
using DotLearn.Enrollment.Models.DTOs;
using DotLearn.Enrollment.Services;
using System.Text.Json;

namespace DotLearn.Enrollment.Workers;

public class PaymentSucceededConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAmazonSQS _sqsClient;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentSucceededConsumer> _logger;

    public PaymentSucceededConsumer(
        IServiceScopeFactory scopeFactory,
        IAmazonSQS sqsClient,
        IConfiguration config,
        ILogger<PaymentSucceededConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _sqsClient = sqsClient;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var queueUrl = _config["SQS:PaymentSucceededQueue"];

                if (string.IsNullOrWhiteSpace(queueUrl))
                {
                    _logger.LogWarning("SQS:PaymentSucceededQueue is not configured.");
                    await Task.Delay(30000, ct);
                    continue;
                }

                var response = await _sqsClient.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = queueUrl,
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 20
                    }, ct);

                if (response?.Messages == null || response.Messages.Count == 0)
                {
                    await Task.Delay(2000, ct);
                    continue;
                }

                foreach (var message in response.Messages)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(message.Body))
                        {
                            _logger.LogWarning("Received empty SQS message body. MessageId: {Id}", message.MessageId);

                            await _sqsClient.DeleteMessageAsync(
                                queueUrl,
                                message.ReceiptHandle,
                                ct);

                            continue;
                        }

                        var evt = JsonSerializer.Deserialize<PaymentSucceededEventDto>(
                            message.Body,
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                        if (evt == null)
                        {
                            _logger.LogWarning("Failed to deserialize PaymentSucceededEventDto. MessageId: {Id}, Body: {Body}",
                                message.MessageId, message.Body);

                            continue;
                        }

                        using var scope = _scopeFactory.CreateScope();
                        var service = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();

                        await service.CreateFromPaymentAsync(evt);

                        await _sqsClient.DeleteMessageAsync(
                            queueUrl,
                            message.ReceiptHandle,
                            ct);

                        _logger.LogInformation(
                            "Processed PaymentSucceeded for message {Id}",
                            message.MessageId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to process message {Id}", message.MessageId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SQS polling error");
                await Task.Delay(5000, ct);
            }
        }
    }
}
