using System.Collections.Generic;
using PB = LiveKit.Proto;

internal enum VideoQuality {
    Low,
    Medium,
    High,
    Off     // NOTE:Thomas: swift코드에 정의 되어 있지 않음
}

internal static class VideoQualityExtension
{
    public static readonly string[] Rids = { "q", "h", "f" };

    private static readonly Dictionary<VideoQuality, PB.VideoQuality> toPBTypeMap = new ()
    {
        { VideoQuality.Low, PB.VideoQuality.Low },
        { VideoQuality.Medium, PB.VideoQuality.Medium },
        { VideoQuality.High, PB.VideoQuality.High },
        { VideoQuality.Off, PB.VideoQuality.Off }   // NOTE:Thomas: swift코드에 정의 되어 있지 않음
    };

    internal static PB.VideoQuality toPBType(this VideoQuality videoQuality)
    {
        return toPBTypeMap[videoQuality];
    }
}

internal static class PBQualityExtension
{
    private static readonly Dictionary<PB.VideoQuality, VideoQuality> ToSDKTypeMap = new()
    {
        { PB.VideoQuality.Low, VideoQuality.Low },
        { PB.VideoQuality.Medium, VideoQuality.Medium },
        { PB.VideoQuality.High, VideoQuality.High },
        { PB.VideoQuality.Off, VideoQuality.Off }   // NOTE:Thomas: swift코드에 정의 되어 있지 않음
    };

    static VideoQuality ToSDKType(this PB.VideoQuality pbVideoQuality)
    {
        return ToSDKTypeMap[pbVideoQuality];
    }

    // HACK:Thomas:swift: C#에서 Enum에 static 함수가 불가하다. -> PBQualityExtension클래스 사용
    internal static PB.VideoQuality From(string rid)
    {
        return rid switch
        {
            "h" => PB.VideoQuality.Medium,
            "q" => PB.VideoQuality.Low,
            _ => PB.VideoQuality.High
        };
    }
}