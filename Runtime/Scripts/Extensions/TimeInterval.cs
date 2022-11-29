using System;

/// Default timeout `TimeInterval`s used throughout the SDK.
internal struct TimeInterval {
    internal const double DefaultCaptureStart = 5;
    internal const double DefaultConnectivity = 10;
    internal const double DefaultPublish = 10;
    internal const double DefaultQuickReconnectRetry = 2;
    // the following 3 timeouts are used for a typical connect sequence
    internal const double DefaultSocketConnect = 10;
    internal const double DefaultJoinResponse = 7;
    internal const double DefaultTransportState = 10;
    // used for validation mode
    internal const double DefaultHTTPConnect = 5;
    internal const double DefaultPublisherDataChannelOpen = 7;
}