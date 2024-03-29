﻿using Astap.Lib.Devices;
using Astap.Lib.Devices.Ascom;
using Shouldly;
using System;
using Xunit;

namespace Astap.Lib.Tests;

public class DeviceTests
{
    [Theory]
    [InlineData(@"telescope://AscomDevice/EQMOD.Telescope#EQMOD ASCOM HEQ5/6", typeof(AscomDevice), DeviceType.Telescope, "EQMOD.Telescope", "EQMOD ASCOM HEQ5/6")]
    [InlineData(@"Focuser://ascomdevice/ASCOM.EAF.Focuser#ZWO Focuser (1)", typeof(AscomDevice), DeviceType.Focuser, "ASCOM.EAF.Focuser", "ZWO Focuser (1)")]
    [InlineData(@"filterWheel://ascomDevice/ASCOM.EFW.FilterWheel#ZWO Filter Wheel #1", typeof(AscomDevice), DeviceType.FilterWheel, "ASCOM.EFW.FilterWheel", "ZWO Filter Wheel #1")]
    [InlineData(@"none://NoneDevice/None", typeof(NoneDevice), DeviceType.None, "None", "")]
    public void GivenAnUriDisplayNameDeviceTypeAndClassAreReturned(string uriString, Type containerType, DeviceType expectedDeviceType, string expectedId, string expectedDisplayName)
    {
        var uri = new Uri(uriString);
        DeviceBase.TryFromUri(uri, out var device).ShouldBeTrue();

        device.GetType().ShouldBe(containerType);
        device.DeviceClass.ShouldBe(device.GetType().Name, StringCompareShould.IgnoreCase);

        device.DeviceType.ShouldBe(expectedDeviceType);
        device.DeviceId.ShouldBe(expectedId);
        device.DisplayName.ShouldBe(expectedDisplayName);
    }
}
