using System;

public interface IPublishOptions {
    public string Name { get; }
}

public struct VideoPublishOptions: IPublishOptions
{
    public string Name { get; } 

    /// preferred encoding parameters
    public VideoEncoding? encoding;
    /// encoding parameters for for screen share
    public VideoEncoding? screenShareEncoding;
    /// true to enable simulcasting, publishes three tracks at different sizes
    /// Unity WebRTC (2.4.0) does not support Simulcast yet. 
    public const bool simulcast = false;  //public bool simulcast;

    public VideoParameters[] simulcastLayers;
    public VideoParameters[] screenShareSimulcastLayers;

    public VideoPublishOptions(string name = null,
                               VideoEncoding? encoding = null,
                               VideoEncoding? screenShareEncoding = null,
                               //bool simulcast = true,
                               VideoParameters[] simulcastLayers = null,
                               VideoParameters[] screenShareSimulcastLayers = null)
    {
        this.Name = name;
        this.encoding = encoding;
        this.screenShareEncoding = screenShareEncoding;
        //this.simulcast = simulcast;
        this.simulcastLayers = simulcastLayers ?? new VideoParameters[0];
        this.screenShareSimulcastLayers = screenShareSimulcastLayers ?? new VideoParameters[0];
    }
}

public struct AudioPublishOptions: IPublishOptions
{
    public string Name { get; }
    public long? bitrate;
    public bool dtx;

    public AudioPublishOptions(string name = null,
                               long? bitrate = null,
                               bool dtx = true)
    {
        this.Name = name;
        this.bitrate = bitrate;
        this.dtx = dtx;
    }
}

public struct DataPublishOptions: IPublishOptions
{
    public string Name { get; }

    public DataPublishOptions(string name = null)
    {
        this.Name = name;
    }
}