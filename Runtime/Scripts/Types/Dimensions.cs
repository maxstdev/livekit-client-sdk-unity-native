using System;
using Unity.WebRTC;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using PB = LiveKit.Proto;

//// use CMVideoDimensions instead of defining our own struct
//public typealias Dimensions = CMVideoDimensions
public partial struct Dimensions
{
    public Int32 Width;
    public Int32 Height;

    public Dimensions(Int32 width, Int32 height)
    {
        Width = width;
        Height = height;
    }
}

// - Static constants

public partial struct Dimensions
{
    public static double AspectRatio169 = 16.0D / 9.0D;
    public static double AspectRatio43 = 4.0D / 3.0D;
    public static Dimensions Zero = new Dimensions(width: 0, height: 0);

    internal static Int32 RenderSafeSize = 8;
	internal static Int32 EncodeSafeSize = 16;
}

// - Equatable

public partial struct Dimensions : IEquatable<Dimensions>
{
    public override int GetHashCode() => base.GetHashCode();

    public bool Equals(Dimensions other)
    {
        if (other == null) return false;
        return (this.Width == other.Width && this.Height == other.Height) ? true : false;
    }

    public override bool Equals(object obj)
    {
        if (obj == null) return false;
        return (obj is Dimensions casted) ? Equals(casted) : false;
    }

    public static bool operator ==(Dimensions lhs, Dimensions rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return object.Equals(lhs, rhs);
        return lhs.Equals(rhs);
    }

    public static bool operator !=(Dimensions lhs, Dimensions rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return !object.Equals(lhs, rhs);
        return !(lhs.Equals(rhs));
    }
}

public partial struct Dimensions
{
    public double AspectRatio
    {
        get
        {
            var w = (double)Width;
            var h = (double)Height;
            return (w > h ? w / h : h / w);
        }
    }

    public Int32 Max { get => Math.Max(Width, Height); }
    public Int32 Sum { get => Width + Height; }
    public Int32 Area { get => Width * Height; }

    Dimensions Swapped() => new Dimensions(width: Height, height: Width);

    Dimensions AspectFit(Int32 size)
    {
        bool c = Width >= Height;
        double r = (c ? (double)Height / (double)Width : (double)Width / (double)Height);
        return new Dimensions(width: c ? size : (Int32)(r * (double)size), height: c ? (Int32)(r * (double)size) : size);
    }

    public VideoParameters[] ComputeSuggestedPresets(bool isScreenShare)
    {
        if (isScreenShare)
        {
            return VideoParameters.PresetsScreenShare;
        }

        if (Math.Abs(AspectRatio - Dimensions.AspectRatio169) < Math.Abs(AspectRatio - Dimensions.AspectRatio43))
        {
            return VideoParameters.Presets169;
        }

        return VideoParameters.Presets43;
    }

    public VideoEncoding ComputeSuggestedPreset(VideoParameters[] presets)
    {
        Debug.Assert(!(presets.Length == 0));
        var result = presets[0].encoding;

        foreach (var preset in presets)
        {
            result = preset.encoding;
            if (preset.dimensions.Width >= Max)
            {
                break;
            }
        }
        return result;
    }

    public RTCRtpEncodingParameters[] Encodings(VideoParameters[] presets)
    {
        var result = new List<RTCRtpEncodingParameters>();

        var notNullPresets = presets.Where(t => t != null);

        foreach (var item in notNullPresets.Select((value, index) => (value, index)))
        {
            var index = item.index;
            var preset = item.value;

            if (index <= VideoQualityExtension.Rids.Length)
            {
                continue;
            }

            var rid = VideoQualityExtension.Rids[index];

            var parameters = Engine.CreateRtpEncodingParameters(
                rid: rid,
                encoding: preset.encoding,
                scaleDown: (double)Max / (double)preset.dimensions.Max);

            result.Add(parameters);

        }

        var encodingParameters = VideoQualityExtension.Rids.Select(rid => result.First(resultItem => resultItem.rid == rid));
        var notNullEncodingParameters = encodingParameters.Where(t => t != null);

        return notNullEncodingParameters.ToArray();
    }

    int ComputeSuggestedPresetIndex(VideoParameters[] presets)
    {
        Debug.Assert(!(presets.Length == 0));
        var result = 0;

        foreach (var preset in presets)
        {
            if (Width >= preset.dimensions.Width && Height >= preset.dimensions.Height)
            {
                result += 1;
            }
        }
        return result;
    }

    internal PB.VideoLayer[] VideoLayers(RTCRtpEncodingParameters[] encodings)
    {
        var activedEncodings = encodings.Where(encoding => encoding.active);
        var dimensions = this;

        var videoLayers = activedEncodings.Select(encoding =>
        {
            var scaleDownBy = encoding.scaleResolutionDownBy ?? 1.0;

            return new LiveKit.Proto.VideoLayer {
                Width = (uint)Math.Ceiling(dimensions.Width / scaleDownBy),
                Height = (uint)Math.Ceiling(dimensions.Height / scaleDownBy),
                Quality = PBQualityExtension.From(rid: encoding.rid),
                Bitrate = (uint)(encoding.maxBitrate ?? 0)
            };
        });

        return videoLayers.ToArray();
    }
}

// - Convert

// TODO:Thomas:선택: iOS 시스템 종속
//extension Dimensions {

//    func toCGSize() -> CGSize {
//        CGSize(width: Int(width), height: Int(height))
//    }

//    func apply(rotation: RTCVideoRotation) -> Dimensions {

//        if ._90 == rotation || ._270 == rotation {
//            return swapped()
//        }

//        return self
//    }
//}

// - Presets

public partial struct Dimensions
{
    // 16:9 aspect ratio presets
    public static Dimensions H90_169 = new Dimensions(width: 160, height: 90);
    public static Dimensions H180_169 = new Dimensions(width: 320, height: 180);
    public static Dimensions H216_169 = new Dimensions(width: 384, height: 216);
    public static Dimensions H360_169 = new Dimensions(width: 640, height: 360);
    public static Dimensions H540_169 = new Dimensions(width: 960, height: 540);
    public static Dimensions H720_169 = new Dimensions(width: 1_280, height: 720);
    public static Dimensions H1080_169 = new Dimensions(width: 1_920, height: 1_080);
    public static Dimensions H1440_169 = new Dimensions(width: 2_560, height: 1_440);
    public static Dimensions H2160_169 = new Dimensions(width: 3_840, height: 2_160);

    // 4:3 aspect ratio presets
    public static Dimensions H120_43 = new Dimensions(width: 160, height: 120);
    public static Dimensions H180_43 = new Dimensions(width: 240, height: 180);
    public static Dimensions H240_43 = new Dimensions(width: 320, height: 240);
    public static Dimensions H360_43 = new Dimensions(width: 480, height: 360);
    public static Dimensions H480_43 = new Dimensions(width: 640, height: 480);
    public static Dimensions H540_43 = new Dimensions(width: 720, height: 540);
    public static Dimensions H720_43 = new Dimensions(width: 960, height: 720);
    public static Dimensions H1080_43 = new Dimensions(width: 1_440, height: 1_080);
    public static Dimensions H1440_43 = new Dimensions(width: 1_920, height: 1_440);
}