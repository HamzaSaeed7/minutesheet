using System.Threading.Channels;

namespace minutesheet.Components.Account
{
    // One queued notification email.
    public sealed record EmailJob(string To, string Subject, string Message, string Link);

    // Hands outbound notification mail to a background sender so a user action
    // (submitting a sheet, approving a step) returns as soon as the data is saved
    // instead of waiting on an SMTP round-trip per recipient.
    public sealed class EmailQueue
    {
        private readonly Channel<EmailJob> _channel =
            Channel.CreateUnbounded<EmailJob>(new UnboundedChannelOptions { SingleReader = true });

        public void Enqueue(EmailJob job) => _channel.Writer.TryWrite(job);

        public IAsyncEnumerable<EmailJob> ReadAllAsync(CancellationToken cancellationToken) =>
            _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
