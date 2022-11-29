using PB = LiveKit.Proto;

public class Identity {
    string identity;
    string publish;

    public Identity(string identity, string publish) {
        this.identity = identity;
        this.publish = publish;
    }
}

public static class PBParticipantInfoExtension
{
    public static Identity ParseIdentity(this PB.ParticipantInfo pbParticipantInfo)
    {
        var segments = pbParticipantInfo.Identity.Split(separator: "#", count: 1);
        string publishSegment = string.Empty;
        if (segments.Length >= 2) {
            publishSegment = segments[1];
        }

        return new Identity(
            identity: segments[0],
            publish: publishSegment
        );
    }
}