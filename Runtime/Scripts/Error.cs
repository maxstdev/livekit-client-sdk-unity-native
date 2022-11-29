
using System;

public enum RoomError
{
    missingroomid,
    invalidurl,
    protocolerror
}

public enum InternalError
{
    State, Parse, Convert, Timeout
}

public enum EngineError
{
    WebRTC, State, TimeOut
}

public enum TrackError
{
    State, Type, Duplicate, Capturer, Publish, Unpublish, TimeOut
}

public enum SignalClientError
{
    State, SocketError, Close, Connect, TimeOut
}

public enum NetworkError
{
    Disconnected, Response
}

public enum TransportError
{
    TimeOut
}

namespace UniLiveKit
{
    namespace ErrorException
    {
        public class EnumException<T> : Exception where T : Enum
        {
            public T type;
            public EnumException(T type, string message = "", Exception innerException = null) : base(message, innerException)
            {
                this.type = type;
            }

            public override string ToString()
            {
                base.ToString();

                if (type is RoomError)
                {
                    return "RoomError";
                }

                return $"name : {type} : {Message}";
            }
        }
    }
}
