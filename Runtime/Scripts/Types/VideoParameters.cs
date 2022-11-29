using System;
using System.Linq;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public partial struct VideoParameters
{
    public Dimensions dimensions;
    public VideoEncoding encoding;

    public VideoParameters(Dimensions dimensions, VideoEncoding encoding)
    {
        this.dimensions = dimensions;
        this.encoding = encoding;
    }
}

// - Computation

public partial struct VideoParameters
{
    struct Layer
    {
        internal Double scaleDownBy;
        internal int fps;

        internal Layer(Double scaleDownBy, int fps)
        {
            this.scaleDownBy = scaleDownBy;
            this.fps = fps;
        }
    }

    List<VideoParameters> DefaultScreenShareSimulcastLayers()
    {
        var layer = new List<Layer> { new Layer(2, 3) };

        var dimensionsWidth = dimensions.Width;
        var dimensionsHeight = dimensions.Height;
        var encodingMaxBitrate = encoding.MaxBitrate;
        var encodingMaxFps = encoding.MaxFps;

        return layer.Select(e =>
        {
            var width = (Int32)Math.Truncate(((double)dimensionsWidth / e.scaleDownBy));
            var height = (Int32)Math.Truncate(((double)dimensionsHeight / e.scaleDownBy));

            var dimensions = new Dimensions(width, height);

            var bitrate2 = (int)((Double)encodingMaxBitrate / (Math.Pow((double)e.scaleDownBy, 2) * (double)encodingMaxFps) / Math.Truncate((double)e.fps));

            var encoding = new VideoEncoding(Math.Max(150_000, bitrate2), e.fps);
            return new VideoParameters(dimensions, encoding);
        }).ToList();
    }

    public VideoParameters[] DefaultSimulcastLayers(bool isScreenShare)
    {
        if (isScreenShare)
        {
            return DefaultScreenShareSimulcastLayers().ToArray();
        }

        if (Math.Abs(dimensions.AspectRatio - Dimensions.AspectRatio169) < Math.Abs(dimensions.AspectRatio - Dimensions.AspectRatio43))
        {
            return VideoParameters.DefaultSimulcastPresets169;
        }
        return VideoParameters.DefaultSimulcastPresets43;
    }
}

// - Presets

public partial struct VideoParameters
{
    // 16:9 aspect ratio

    static readonly VideoParameters PresetH90_169 = new VideoParameters(
        dimensions: Dimensions.H90_169,
        encoding: new VideoEncoding(maxBitrate: 60_000, maxFps: 15)
    );

    static readonly VideoParameters PresetH180_169 = new VideoParameters(
        dimensions: Dimensions.H180_169,
        encoding: new VideoEncoding(maxBitrate: 120_000, maxFps: 15)
    );

    static readonly VideoParameters PresetH216_169 = new VideoParameters(
        dimensions: Dimensions.H216_169,
        encoding: new VideoEncoding(maxBitrate: 180_000, maxFps: 15)
    );

    static readonly VideoParameters PresetH360_169 = new VideoParameters(
        dimensions: Dimensions.H360_169,
        encoding: new VideoEncoding(maxBitrate: 300_000, maxFps: 20)
    );

    static readonly VideoParameters PresetH540_169 = new VideoParameters(
        dimensions: Dimensions.H540_169,
        encoding: new VideoEncoding(maxBitrate: 600_000, maxFps: 25)
    );

    static readonly VideoParameters PresetH720_169 = new VideoParameters(
        dimensions: Dimensions.H720_169,
        encoding: new VideoEncoding(maxBitrate: 2_000_000, maxFps: 30)
    );

    static readonly VideoParameters PresetH1080_169 = new VideoParameters(
        dimensions: Dimensions.H1080_169,
        encoding: new VideoEncoding(maxBitrate: 3_000_000, maxFps: 30)
    );

    static readonly VideoParameters PresetH1440_169 = new VideoParameters(
        dimensions: Dimensions.H1440_169,
        encoding: new VideoEncoding(maxBitrate: 5_000_000, maxFps: 30)
    );

    static readonly VideoParameters PresetH2160_169 = new VideoParameters(
        dimensions: Dimensions.H2160_169,
        encoding: new VideoEncoding(maxBitrate: 8_000_000, maxFps: 30)
    );

    // 4:3 aspect ratio

    static readonly VideoParameters PresetH120_43 = new VideoParameters(
        dimensions: Dimensions.H120_43,
        encoding: new VideoEncoding(maxBitrate: 80_000, maxFps: 15)
    );

    static readonly VideoParameters PresetH180_43 = new VideoParameters(
        dimensions: Dimensions.H180_43,
        encoding: new VideoEncoding(maxBitrate: 100_000, maxFps: 15)
    );

    static readonly VideoParameters PresetH240_43 = new VideoParameters(
        dimensions: Dimensions.H240_43,
        encoding: new VideoEncoding(maxBitrate: 150_000, maxFps: 15)
    );

    static readonly VideoParameters PresetH360_43 = new VideoParameters(
        dimensions: Dimensions.H360_43,
        encoding: new VideoEncoding(maxBitrate: 225_000, maxFps: 20)
    );

    static readonly VideoParameters PresetH480_43 = new VideoParameters(
        dimensions: Dimensions.H480_43,
        encoding: new VideoEncoding(maxBitrate: 300_000, maxFps: 20)
    );

    static readonly VideoParameters PresetH540_43 = new VideoParameters(
        dimensions: Dimensions.H540_43,
        encoding: new VideoEncoding(maxBitrate: 450_000, maxFps: 25)
    );

    static readonly VideoParameters PresetH720_43 = new VideoParameters(
        dimensions: Dimensions.H720_43,
        encoding: new VideoEncoding(maxBitrate: 1_500_000, maxFps: 30)
    );

    static readonly VideoParameters PresetH1080_43 = new VideoParameters(
        dimensions: Dimensions.H1080_43,
        encoding: new VideoEncoding(maxBitrate: 2_500_000, maxFps: 30)
    );

    static readonly VideoParameters PresetH1440_43 = new VideoParameters(
        dimensions: Dimensions.H1440_43,
        encoding: new VideoEncoding(maxBitrate: 3_500_000, maxFps: 30)
    );

    // Screen share

    static readonly VideoParameters PresetScreenShareH360FPS3 = new VideoParameters(
        dimensions: Dimensions.H360_169,
        encoding: new VideoEncoding(maxBitrate: 200_000, maxFps: 3)
    );

    static readonly VideoParameters PresetScreenShareH720FPS5 = new VideoParameters(
        dimensions: Dimensions.H720_169,
        encoding: new VideoEncoding(maxBitrate: 400_000, maxFps: 5)
    );

    static readonly VideoParameters PresetScreenShareH720FPS15 = new VideoParameters(
        dimensions: Dimensions.H720_169,
        encoding: new VideoEncoding(maxBitrate: 1_000_000, maxFps: 15)
    );

    // Maxst Custom: Add
    static readonly VideoParameters PresetScreenShareH720FPS30 = new VideoParameters(
        dimensions: Dimensions.H720_169,
        encoding: new VideoEncoding(maxBitrate: 2_000_000, maxFps: 30)
    );

    static readonly VideoParameters PresetScreenShareH1080FPS15 = new VideoParameters(
        dimensions: Dimensions.H1080_169,
        encoding: new VideoEncoding(maxBitrate: 1_500_000, maxFps: 15)
    );

    static readonly VideoParameters PresetScreenShareH1080FPS30 = new VideoParameters(
        dimensions: Dimensions.H1080_169,
        encoding: new VideoEncoding(maxBitrate: 3_000_000, maxFps: 30)
    );
}

public partial struct VideoParameters
{
    public static readonly VideoParameters[] Presets43 = {
        PresetH120_43,
        PresetH180_43,
        PresetH240_43,
        PresetH360_43,
        PresetH480_43,
        PresetH540_43,
        PresetH720_43,
        PresetH1080_43,
        PresetH1440_43
    };

    public static readonly VideoParameters[] Presets169 = new[] {
        PresetH90_169,
        PresetH180_169,
        PresetH216_169,
        PresetH360_169,
        PresetH540_169,
        PresetH720_169,
        PresetH1080_169,
        PresetH1440_169,
        PresetH2160_169
    };

    public static readonly VideoParameters[] PresetsScreenShare = {
        PresetScreenShareH360FPS3,
        PresetScreenShareH720FPS5,
        PresetScreenShareH720FPS15,
        PresetScreenShareH720FPS30, // Maxst Custom: Add
        PresetScreenShareH1080FPS15,
        PresetScreenShareH1080FPS30
    };

    public static readonly VideoParameters[] DefaultSimulcastPresets169 = {
        PresetH180_169,
        PresetH360_169
    };

    public static readonly VideoParameters[] DefaultSimulcastPresets43 = {
        PresetH180_43,
        PresetH360_43
    };
}

public partial struct VideoParameters : IComparable<VideoParameters>, IComparable
{
    public int CompareTo(VideoParameters other)
    {
        if (this.dimensions.Area == other.dimensions.Area)
        {
            return this.encoding.CompareTo(other.encoding);
        }

        return this.dimensions.Area.CompareTo(other.dimensions.Area);
    }

    int IComparable.CompareTo(object obj)
    {
        if (!(obj is VideoParameters))
            throw new ArgumentException("Argument is not a VideoParameters", "obj");

        VideoParameters videoParameters = (VideoParameters)obj;

        return this.CompareTo(videoParameters);
    }

    public static bool operator <(VideoParameters lhs, VideoParameters rhs) => lhs.CompareTo(rhs) < 0;
    public static bool operator >(VideoParameters lhs, VideoParameters rhs) => lhs.CompareTo(rhs) > 0;
}

public partial struct VideoParameters : IEquatable<VideoParameters>
{
    public override int GetHashCode() => base.GetHashCode();

    public bool Equals(VideoParameters other)
    {
        if (other == null) return false;
        return (this.dimensions == other.dimensions && this.encoding == other.encoding) ? true : false;
    }

    public override bool Equals(object obj)
    {
        if (obj == null) return false;
        return (obj is VideoParameters casted) ? Equals(casted) : false;
    }

    public static bool operator ==(VideoParameters lhs, VideoParameters rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return object.Equals(lhs, rhs);
        return lhs.Equals(rhs);
    }

    public static bool operator !=(VideoParameters lhs, VideoParameters rhs)
    {
        if (((object)lhs) == null || ((object)rhs) == null) return !object.Equals(lhs, rhs);
        return !(lhs.Equals(rhs));
    }
}