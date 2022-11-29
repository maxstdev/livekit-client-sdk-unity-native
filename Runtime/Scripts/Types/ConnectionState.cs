using System;

public enum ReconnectMode
{
    Quick,
    Full
}

public partial struct ConnectionState
{
    public States State { get; private set; }
    public DisconnectReason? Reason { get; private set; }
    
    public enum States
    {
        Disconnected,
        Connecting,
        Reconnecting,
        Connected
    }

     public ConnectionState(States state, DisconnectReason? reason = null)
    {
        this.State = state;
        this.Reason = reason;
    }

    public void Update(States state, DisconnectReason? reason = null)
    {
        this.State = state;
        this.Reason = reason;
    }
}

public partial struct ConnectionState : IEquatable<ConnectionState>
{
    public override int GetHashCode() => base.GetHashCode();

    public bool Equals(ConnectionState other)
    {
        if (other == null) return false;
        return (this.State == other.State &&
               this.Reason == other.Reason) ? true : false;
    }

    public override bool Equals(object obj)
    {
        if (obj == null) return false;
        return (obj is ConnectionState casted) ? Equals(casted) : false;
    }

    public static bool operator ==(ConnectionState lhs, ConnectionState rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return object.Equals(lhs, rhs);
        return lhs.Equals(rhs);
    }

    public static bool operator !=(ConnectionState lhs, ConnectionState rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return !object.Equals(lhs, rhs);
        return !(lhs.Equals(rhs));
    }
}

// NOTE:Thomas: C# Enum에서는 Property를 추가가 불가하여 Method로 구현
// FIXME:Thomas: 차후  ConnectionState 내 Property로 변경 필요
public static class ConnectionStateEquatable
{
    public static bool isConnected(this ConnectionState.States connectionState)
    {
        return connectionState switch
        {
            ConnectionState.States.Connected => true,
            _ => false
        };
    }

    public static bool isReconnecting(this ConnectionState.States connectionState)
    {
        return connectionState switch
        {
            ConnectionState.States.Reconnecting => true,
            _ => false
        };
    }

    public static bool isDisconnected(this ConnectionState.States connectionState)
    {
        return connectionState switch
        {
            ConnectionState.States.Disconnected => true,
            _ => false
        };
    }

    // TODO:Thomas:필수: 차후에
    //    public var disconnectedWithError: Error? {
    //        guard case .disconnected(let reason) = self,
    //              case .networkError(let error) = reason else { return nil }
    //        return error
    //    }
}

public enum DisconnectReason
{
    user, // User initiated
    networkError
}

public static class DisconnectReasonException
{
    public static Exception Reason(this DisconnectReason _) => exceptionBuffer;
    public static Exception exceptionBuffer;

    public static DisconnectReason Reason(this DisconnectReason reason, Exception error = null)
    {
        exceptionBuffer = error;
        return reason;
    }
}


// NOTE:Thomas: C# Enum에서는 암묵적인 == 연산자를 제공한다고 한다. 없다면 구현
//extension DisconnectReason: Equatable {

//    public static func == (lhs: DisconnectReason, rhs: DisconnectReason) -> Bool {
//        lhs.isEqual(to: rhs)
//    }

//    public func isEqual(to rhs: DisconnectReason, includingAssociatedValues: Bool = true) -> Bool {
//        switch (self, rhs) {
//        case (.user, .user): return true
//        case (.networkError, .networkError): return true
//        default: return false
//        }
//    }

//    var error: Error? {
//        if case .networkError(let error) = self {
//            return error
//        }

//        return nil
//    }
//}

interface ReconnectableState
{
    public ReconnectMode? reconnectMode { get; }
    public ConnectionState connectionState { get; }
}
