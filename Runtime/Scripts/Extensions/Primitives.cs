using System;
using Sid = System.String;

public static class StringExtension
{
    public static (Sid sid, string trackId) Unpack(this string str)
    {
        var parts = str.Split('|');
        if (parts.Length == 2)
        {
            return (parts[0], parts[1]);
        }
        return (str, "");
    }
}

internal static class BoolExtension
{
    internal static string ToLowerCaseString(this bool value) => value ? "true" : "false";
}

public static class UriExtension
{
    public static bool isSecure(this Uri uri) => uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == "wss";
}
