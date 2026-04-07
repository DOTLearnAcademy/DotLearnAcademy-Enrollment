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
                var response = await _sqsClient.ReceiveMessageAsync(
                    new ReceiveMessageRequest
                    {
                        QueueUrl = _config["SQS:PaymentSucceededQueue"],
                        MaxNumberOfMessages = 10,
                        WaitTimeSeconds = 20
                    }, ct);

                foreach (var message in response.Messages)
                {
                    try
                    {
                        var evt = JsonSerializer.Deserialize<PaymentSucceededEventDto>(
                            message.Body,
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                        if (evt != null)
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var service = scope.ServiceProvider
                                .GetRequiredService<IEnrollmentService>();
                            await service.CreateFromPaymentAsync(evt);
                        }

                        await _sqsClient.DeleteMessageAsync(
                            _config["SQS:PaymentSucceededQueue"],
                            message.ReceiptHandle, ct);

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
