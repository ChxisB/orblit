using System;
using Orblit;
using JetBrains.Rider.PathLocator;
using Newtonsoft.Json;
using OS = OrblitTools.Utils.OS;

namespace OrblitTools.Ides.Rider;

public class RiderLocatorEnvironment : IRiderLocatorEnvironment
{
    public JetBrains.Rider.PathLocator.OS CurrentOS
    {
        get
        {
            if (OS.IsWindows)
                return JetBrains.Rider.PathLocator.OS.Windows;
            if (OS.IsMacOS) return JetBrains.Rider.PathLocator.OS.MacOSX;
            if (OS.IsUnixLike) return JetBrains.Rider.PathLocator.OS.Linux;
            return JetBrains.Rider.PathLocator.OS.Other;
        }
    }

    public T? FromJson<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json);
    }

    public void Info(string message, Exception? e = null)
    {
        if (e == null)
            Orb.Print(message);
        else
            Orb.Print(message, e);
    }

    public void Warn(string message, Exception? e = null)
    {
        if (e == null)
            Orb.PushWarning(message);
        else
            Orb.PushWarning(message, e);
    }

    public void Error(string message, Exception? e = null)
    {
        if (e == null)
            Orb.PushError(message);
        else
            Orb.PushError(message, e);
    }

    public void Verbose(string message, Exception? e = null)
    {
        // do nothing, since IDK how to write only to the log, without spamming the output
    }
}
