using System;

public partial struct ParticipantPermissions
{
    bool CanSubscribe;
    /// allow participant to publish new tracks to room
    bool CanPublish;
    /// allow participant to publish data
    bool CanPublishData;
    /// indicates that it's hidden to others
    bool Hidden;
    /// indicates it's a recorder instance
    bool Recorder;

    public ParticipantPermissions(bool canSubscribe = false,
                                  bool canPublish = false,
                                  bool canPublishData = false,
                                  bool hidden = false,
                                  bool recorder = false)
    {
        this.CanSubscribe = canSubscribe;
        this.CanPublish = canPublish;
        this.CanPublishData = canPublishData;
        this.Hidden = hidden;
        this.Recorder = recorder;
    }
}

public partial struct ParticipantPermissions : IEquatable<ParticipantPermissions>
{
    public override int GetHashCode() => base.GetHashCode();

    public bool Equals(ParticipantPermissions other)
    {
        if (other == null) return false;
        return (this.CanSubscribe == other.CanSubscribe &&
               this.CanPublish == other.CanPublish &&
               this.CanPublishData == other.CanPublishData &&
               this.Hidden == other.Hidden &&
               this.Recorder == other.Recorder) ? true : false;
    }

    public override bool Equals(object obj)
    {
        if (obj == null) return false;
        return (obj is ParticipantPermissions casted) ? Equals(casted) : false;
    }

    public static bool operator ==(ParticipantPermissions lhs, ParticipantPermissions rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return object.Equals(lhs, rhs);
        return lhs.Equals(rhs);
    }

    public static bool operator !=(ParticipantPermissions lhs, ParticipantPermissions rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return !object.Equals(lhs, rhs);
        return !(lhs.Equals(rhs));
    }
}

namespace LiveKit.Proto
{
    public partial class ParticipantPermission
    {
        public ParticipantPermissions toLKType()
        {
            return new ParticipantPermissions(canSubscribe: this.CanSubscribe,
                                              canPublish: this.CanPublish,
                                              canPublishData: this.CanPublishData,
                                              hidden: this.Hidden,
                                              recorder: this.Recorder);
        }
    }
}