﻿using System;

namespace Astap.Lib.Devices;

public interface IDeviceDriver : IDisposable
{
    public string Name { get; }

    public string? Description { get; }

    public string? DriverInfo { get; }

    public string? DriverVersion { get; }

    public DeviceType DriverType { get; }

    public bool Connected { get; set; }

    event EventHandler<DeviceConnectedEventArgs>? DeviceConnectedEvent;
}
