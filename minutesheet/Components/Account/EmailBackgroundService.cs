namespace minutesheet.Components.Account
{
    // Drains the outbound mail queue off the request path. SmtpEmailSender already
    // swallows and logs send failures, so one bad address can't stop the loop.
    public sealed class EmailBackgroundService(
        EmailQueue queue,
        SmtpEmailSender sender,
        ILogger<EmailBackgroundService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var job in queue.ReadAllAsync(stoppingToken))
                {
                    await sender.SendSheetNotificationAsync(job.To, job.Subject, job.Message, job.Link,
                        job.AttachmentBytes, job.AttachmentName);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email background sender stopped unexpectedly.");
            }
        }
    }
}
