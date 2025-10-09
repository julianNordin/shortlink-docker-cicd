namespace ShortLink.Api.Services;

// Its own type so the endpoint can answer this one case specifically. Catching the plain
// InvalidOperationException it replaced would also have swallowed unrelated bugs and
// reported them to the caller as "try again later", which they are not.
public class ShortCodeExhaustedException : Exception
{
    public ShortCodeExhaustedException(int attempts)
        : base($"Could not allocate a free short code in {attempts} attempts.")
    {
        Attempts = attempts;
    }

    public int Attempts { get; }
}
