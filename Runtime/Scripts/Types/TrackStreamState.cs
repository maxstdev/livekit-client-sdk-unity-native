using PB = LiveKit.Proto;

public enum StreamState {
    Paused,
    Active
}

public static class PBStreamStateExtension
{
    public static StreamState toLKType(this PB.StreamState pbStreamState)
    {
        return pbStreamState switch
        {
            PB.StreamState.Active => StreamState.Active,
            _ => StreamState.Paused
        };
    }
}
