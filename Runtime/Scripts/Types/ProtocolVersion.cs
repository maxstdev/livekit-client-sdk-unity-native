using System;

public enum ProtocolVersion
{
    v2 = 2,
    v3 = 3,
    v4 = 4,
    v5 = 5,
    v6 = 6,
    v7 = 7,
    v8 = 8
}

public static class ProtocolVersionCustomStringConvertible
{
    public static string ToIntString(this ProtocolVersion protocolVersion)
    {
        return ((int)protocolVersion).ToString();
    }
}