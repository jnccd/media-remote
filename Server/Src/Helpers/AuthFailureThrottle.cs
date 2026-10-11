namespace Server.Helpers;

/// <summary>
/// Rate-limits *failed* authentication, per client address.
///
/// The token scheme is a pure guessing oracle: anyone can mint a token from a candidate
/// password and the server answers 401 ("wrong") or 200 ("right") after a single AES-GCM
/// decrypt. The PBKDF2 that is supposed to protect the password runs on the attacker's
/// machine, over a salt that is fixed and public, so it costs them almost nothing while it
/// costs us nothing at all - which is thousands of attempts a minute against one address.
/// So: after a failure, an address must wait before its next attempt is even looked at, and
/// the wait doubles with each further failure up to a cap.
///
/// This is deliberately one-sided. Only *failures* cost anything; a correct token is never
/// delayed and clears the address's record. So the normal path - authenticate once per
/// connection, then nothing - is completely unchanged.
///
/// State is in-memory and per-process, which is enough for a server that is one process and
/// whose worst case on restart is forgetting some backoff.
/// </summary>
[RegisterImplementation(ServiceRegisterType.Singleton, typeof(AuthFailureThrottle))]
public sealed class AuthFailureThrottle
{
    /// <summary>Wait imposed after the first failure; doubles with each further failure.</summary>
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Ceiling on the backoff, kept short on purpose. The UI authenticates once and then
    /// reconnects on a fixed 2s timer, so this is also the worst case a real user waits
    /// (once) after correcting a mistyped password. A guesser, meanwhile, gets ~4 attempts a
    /// minute per address instead of thousands.
    /// </summary>
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(15);

    /// <summary>How long a failure record is kept. Only affects memory housekeeping.</summary>
    private static readonly TimeSpan IdleExpiry = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Hard cap on tracked addresses, so a client cycling through source addresses (which an
    /// IPv6 /64 makes cheap) cannot grow the table without bound. The coldest records go first.
    /// </summary>
    private const int MaxTrackedAddresses = 1024;

    private sealed class Entry
    {
        public int Failures { get; set; }
        public DateTimeOffset NextAttempt { get; set; }
        public DateTimeOffset LastSeen { get; set; }
    }

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>
    /// The address an attempt is attributed to: the socket's peer, never X-Forwarded-For.
    /// Nothing sits in front of this server, so that header would be attacker-controlled and
    /// would let anyone reset their own backoff by inventing a new value per request.
    /// </summary>
    public static string AddressOf(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
            return "unknown";

        // A dual-stack socket reports an IPv4 peer as ::ffff:a.b.c.d. That is the same client
        // as a.b.c.d, and it should not get a second budget by alternating between the two
        // spellings of its own address.
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString();
    }

    /// <summary>
    /// How long <paramref name="address"/> must still wait; <see cref="TimeSpan.Zero"/> means
    /// it may attempt now.
    /// </summary>
    public TimeSpan RetryAfter(string address)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(address, out var entry))
                return TimeSpan.Zero;

            var remaining = entry.NextAttempt - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Records a failed attempt. Callers must not call this for attempts they refused because
    /// of <see cref="RetryAfter"/>: those are not guesses, and counting them would let a
    /// client's reconnect loop escalate its own backoff without anyone guessing anything.
    /// </summary>
    public void RecordFailure(string address)
    {
        lock (_gate)
        {
            Prune();

            if (!_entries.TryGetValue(address, out var entry))
                _entries[address] = entry = new Entry();

            entry.Failures++;
            entry.LastSeen = DateTimeOffset.UtcNow;

            var doubled = BaseDelay.TotalMilliseconds * Math.Pow(2, Math.Min(entry.Failures - 1, 8));
            var delay = TimeSpan.FromMilliseconds(Math.Min(doubled, MaxDelay.TotalMilliseconds));
            entry.NextAttempt = entry.LastSeen + delay;
        }
    }

    /// <summary>A correct token proves the address is not guessing, so its record goes away.</summary>
    public void RecordSuccess(string address)
    {
        lock (_gate)
            _entries.Remove(address);
    }

    /// <summary>Must be called with <see cref="_gate"/> held.</summary>
    private void Prune()
    {
        if (_entries.Count < MaxTrackedAddresses)
            return;

        var cutoff = DateTimeOffset.UtcNow - IdleExpiry;
        foreach (var address in _entries.Where(e => e.Value.LastSeen < cutoff).Select(e => e.Key).ToList())
            _entries.Remove(address);

        if (_entries.Count < MaxTrackedAddresses)
            return;

        foreach (var address in _entries.OrderBy(e => e.Value.LastSeen)
                     .Take(_entries.Count - MaxTrackedAddresses + 1)
                     .Select(e => e.Key)
                     .ToList())
        {
            _entries.Remove(address);
        }
    }
}
