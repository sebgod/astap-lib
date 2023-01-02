/*

MIT License

Copyright (c) 2018 Andy Galasso

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using Astap.Lib.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Astap.Lib.Devices.Guider;

public class SettleProgress
{
    public bool Done { get; internal set; }
    public double Distance { get; internal set; }
    public double SettlePx { get; internal set; }
    public double Time { get; internal set; }
    public double SettleTime { get; internal set; }
    public int Status { get; internal set; }
    public string? Error { get; internal set; }
}

public class GuideStats
{
    public double TotalRMS { get; internal set; }
    public double RaRMS { get; internal set; }
    public double DecRMS { get; internal set; }
    public double PeakRa { get; internal set; }
    public double PeakDec { get; internal set; }

    public GuideStats Clone() => (GuideStats)MemberwiseClone();
}

public class SettleRequest
{
    public SettleRequest(double pixels, double time, double timeout)
    {
        Pixels = pixels;
        Time = time;
        Timeout = timeout;
    }

    public double Pixels { get; set; }
    public double Time { get; set; }
    public double Timeout { get; set; }
}

[Serializable]
public class GuiderException : Exception
{
    public GuiderException(string message) : base(message) { }
    public GuiderException(string message, Exception? inner) : base(message, inner) { }

    protected GuiderException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
    {

    }
}

public interface IGuider : IDeviceDriver
{
    /// <summary>
    /// support raw JSONRPC method invocation. Generally you won't need to
    /// use this function as it is much more convenient to use the higher-level methods below
    /// </summary>
    /// <param name="method"></param>
    /// <param name="params"></param>
    /// <returns></returns>
    JsonDocument Call(string method, params object[] @params);

    /// <summary>
    /// Start guiding with the given settling parameters. PHD2 takes care of looping exposures,
    /// guide star selection, and settling. Call <see cref="TryGetSettleProgress(out SettleProgress?)"/> periodically to see when settling
    /// is complete.
    /// </summary>
    /// <param name="settlePixels">settle threshold in pixels</param>
    /// <param name="settleTime">settle time in seconds</param>
    /// <param name="settleTimeout">settle timeout in seconds</param>
    void Guide(double settlePixels, double settleTime, double settleTimeout);

    /// <summary>
    /// Dither guiding with the given dither amount and settling parameters. Call <see cref="TryGetSettleProgress(out SettleProgress?)"/> or <see cref="IsSettling()"/>
    /// periodically to see when settling is complete.
    /// </summary>
    /// <param name="ditherPixels"></param>
    /// <param name="settlePixels"></param>
    /// <param name="settleTime"></param>
    /// <param name="settleTimeout"></param>
    void Dither(double ditherPixels, double settlePixels, double settleTime, double settleTimeout, bool raOnly = false);

    /// <summary>
    /// CHecks if phd2 is currently looping exposures
    /// </summary>
    /// <returns></returns>
    bool IsLooping();

    /// <summary>
    /// Check if guider is currently in the process of settling after a Guide or Dither.
    /// A simplified version of <see cref="TryGetSettleProgress(out SettleProgress?)"/>
    /// </summary>
    /// <returns>true if settling is in progress.</returns>
    bool IsSettling();

    /// <summary>
    /// Returns true if settling is in progress and additional information in <paramref name="settleProgress"/>
    /// </summary>
    /// <param name="settleProgress"></param>
    /// <returns>true if still settling</returns>
    public bool TryGetSettleProgress([NotNullWhen(true)] out SettleProgress? settleProgress);

    // Get the guider statistics since guiding started. Frames captured while settling is in progress
    // are excluded from the stats.
    GuideStats? GetStats();

    // stop looping and guiding
    void StopCapture(uint timeoutSeconds = 10, Action<TimeSpan>? sleep = null);

    /// <summary>
    /// start looping exposures
    /// </summary>
    /// <param name="timeoutSeconds">timeout after looping attempt is cancelled</param>
    bool Loop(uint timeoutSeconds = 10, Action<TimeSpan>? sleep = null);

    /// <summary>
    /// get the guider pixel scale in arc-seconds per pixel
    /// </summary>
    /// <returns>pixel scale of the guiding camera in arc-seconds per pixel</returns>
    double PixelScale();

    /// <summary>
    /// returns camera size in width, heiight (pixels)
    /// </summary>
    /// <returns>camera dimensions in pixel</returns>
    public (int width, int height)? CameraFrameSize();

    /// <summary>
    /// When true, <paramref name="dim"/> contains the image dimensions of the guiding exposure,
    /// <see cref="PixelScale()"/> and <see cref="CameraFrameSize()"/>.
    /// Might still throw exceptions when not connected.
    /// </summary>
    /// <param name="dim"></param>
    /// <returns>true if image dimensions could be obtained.</returns>
    public bool TryGetImageDim([NotNullWhen(true)] out ImageDim? dim)
        => (dim = CameraFrameSize() is var(width, height) && PixelScale() is var pixelScale and > 0
            ? new ImageDim(pixelScale, width, height)
            : default
        ) is not null;

    /// <summary>
    /// get the exposure time of each looping exposure.
    /// </summary>
    /// <returns>exposure time</returns>
    TimeSpan ExposureTime();

    /// <summary>
    /// get a list of the Equipment Profile names
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<string> GetEquipmentProfiles();

    /// <summary>
    /// Tries to obtain the active profile, useful for quick self-discovery.
    ///
    /// Assumes an active connection.
    /// </summary>
    /// <param name="activeProfileName"></param>
    /// <returns>true if <paramref name="activeProfileName"/> is the active profile (and not null)</returns>
    bool TryGetActiveProfileName([NotNullWhen(true)] out string? activeProfileName);

    /// <summary>
    /// connect the the specified profile as constructed.
    /// </summary>
    void ConnectEquipment();

    /// <summary>
    /// disconnect equipment
    /// </summary>
    void DisconnectEquipment();

    /// <summary>
    /// get the AppState (https://github.com/OpenPHDGuiding/phd2/wiki/EventMonitoring#appstate)
    /// and current guide error
    /// </summary>
    /// <param name="appState">application runtime state</param>
    /// <param name="avgDist">a smoothed average of the guide distance in pixels</param>
    void GetStatus(out string? appState, out double avgDist);

    /// <summary>
    /// check if currently guiding
    /// </summary>
    /// <returns></returns>
    bool IsGuiding();

    /// <summary>
    /// pause guiding (looping exposures continues)
    /// </summary>
    void Pause();

    /// <summary>
    /// un-pause guiding
    /// </summary>
    void Unpause();

    /// <summary>
    /// save the current guide camera frame (FITS format), returning the name of the file.
    /// The caller will need to remove the file when done.
    /// THe implementation will copy the output file to <paramref name="outputFolder"/> and delete the temporary file created by the guider.
    /// </summary>
    /// <returns>the full path of the output file if successfully captured.</returns>
    string? SaveImage(string outputFolder);

    /// <summary>
    /// Event that is triggered when an unknown event is received from the guiding application.
    /// </summary>
    event EventHandler<UnhandledEventArgs>? UnhandledEvent;

    /// <summary>
    /// Event that is triggered when an exception occurs.
    /// </summary>
    event EventHandler<GuidingErrorEventArgs>? GuidingErrorEvent;
}

public abstract class GuiderEventArgs : EventArgs
{
    public GuiderEventArgs(GuiderDevice device, string? profile)
    {
        Device = device;
        Profile = profile;
    }

    public GuiderDevice Device { get; }

    public string? Profile { get; }

}

public class UnhandledEventArgs : GuiderEventArgs
{
    public UnhandledEventArgs(GuiderDevice device, string? profile, string @event, string payload)
        : base(device, profile)
    {
        Event = @event;
        Payload = payload;
    }

    public string Event { get; }

    public string Payload { get; }
}

public class GuidingErrorEventArgs : GuiderEventArgs
{
    public GuidingErrorEventArgs(GuiderDevice device, string? profile, string msg, Exception? ex = null)
        : base(device, profile)
    {
        Message = msg;
        Exception = ex;
    }

    public string Message { get; }

    public Exception? Exception { get; }
}

class Accum
{
    uint n;
    double a;
    double q;
    double peak;

    public Accum() {
        Reset();
    }
    public void Reset() {
        n = 0;
        a = q = peak = 0;
    }
    public void Add(double x) {
        double ax = Math.Abs(x);
        if (ax > peak) peak = ax;
        ++n;
        double d = x - a;
        a += d / n;
        q += (x - a) * d;
    }
    public double Mean() {
        return a;
    }
    public double Stdev() {
        return n >= 1 ? Math.Sqrt(q / n) : 0.0;
    }
    public double Peak() {
        return peak;
    }
}