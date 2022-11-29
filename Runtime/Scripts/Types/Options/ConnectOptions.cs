using System;
using Unity.WebRTC;

/// Options used when establishing a connection.
public partial struct ConnectOptions
{
    /// Automatically subscribe to ``RemoteParticipant``'s tracks.
    /// Defaults to true.
    public bool autoSubscribe;
    public RTCConfiguration rtcConfiguration;
    /// LiveKit server protocol version to use. Generally, it's not recommended to change this.
    public ProtocolVersion protocolVersion;
    /// Providing a string will make the connection publish-only, suitable for iOS Broadcast Upload Extensions.
    /// The string can be used to identify the publisher.
    public string publishOnlyMode;

    public ConnectOptions(bool? autoSubscribe = null,                   // true
                          RTCConfiguration? rtcConfiguration = null,    // RTCConfigurationExtension.liveKitDefault()
                          string publishOnlyMode = null,
                          ProtocolVersion protocolVersion = ProtocolVersion.v8)
    {
        this.autoSubscribe = autoSubscribe ?? true;
        this.rtcConfiguration = rtcConfiguration ?? RTCConfigurationExtension.liveKitDefault();
        this.publishOnlyMode = publishOnlyMode;
        this.protocolVersion = protocolVersion;
    }
}

public partial struct ConnectOptions : IEquatable<ConnectOptions>
{
    public override int GetHashCode() => base.GetHashCode();

    public bool Equals(ConnectOptions other)
    {
        if (other == null) return false;
        return (this.autoSubscribe == other.autoSubscribe
                && this.rtcConfiguration.Equals(other.rtcConfiguration)
                && this.protocolVersion.Equals(other.protocolVersion)
                && this.publishOnlyMode.Equals(other.publishOnlyMode)) ? true : false;
    }

    public override bool Equals(object obj)
    {
        if (obj == null) return false;
        return (obj is ConnectOptions casted) ? Equals(casted) : false;
    }

    public static bool operator ==(ConnectOptions lhs, ConnectOptions rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return object.Equals(lhs, rhs);
        return lhs.Equals(rhs);
    }

    public static bool operator !=(ConnectOptions lhs, ConnectOptions rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return !object.Equals(lhs, rhs);
        return !(lhs.Equals(rhs));
    }
}

public partial struct ConnectOptions
{
    ConnectOptions CopyWith(bool? autoSubscribe = null,
                            RTCConfiguration? rtcConfiguration = null,
                            ProtocolVersion? protocolVersion = null)
    {
        return new ConnectOptions(autoSubscribe: autoSubscribe ?? this.autoSubscribe,
                                  rtcConfiguration: rtcConfiguration ?? this.rtcConfiguration,
                                  protocolVersion: protocolVersion ?? this.protocolVersion);
    }
}