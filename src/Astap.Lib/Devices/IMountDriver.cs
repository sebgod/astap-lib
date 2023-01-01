﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Astap.Lib.Astrometry;
using Astap.Lib.Astrometry.SOFA;
using Astap.Lib.Sequencing;
using static Astap.Lib.Astrometry.CoordinateUtils;

namespace Astap.Lib.Devices;

public interface IMountDriver : IDeviceDriver
{
    bool CanSetTracking { get; }

    bool CanSetSideOfPier { get; }

    bool CanPark { get; }

    bool CanSetPark { get; }

    bool CanUnpark { get; }

    bool CanSlew { get; }

    bool CanSlewAsync { get; }

    TrackingSpeed TrackingSpeed { get; set; }

    IReadOnlyCollection<TrackingSpeed> TrackingSpeeds { get; }

    EquatorialCoordinateType EquatorialSystem { get; }

    bool Tracking { get; set; }

    bool AtHome { get; }

    bool AtPark { get; }

    // returns true if park command was accepted
    bool Park();

    bool Unpark();

    bool IsSlewing { get; }

    /// <summary>
    /// Slews to given equatorial coordinates (RA, Dec) in the mounts native epoch, <see cref="EquatorialSystem"/>.
    /// </summary>
    /// <param name="ra">RA in hours (0..24)</param>
    /// <param name="dec">Declination in degrees (-90..90)</param>
    /// <returns>True if slewing operation was accepted and mount is slewing</returns>
    bool SlewRaDecAsync(double ra, double dec);

    /// <summary>
    /// Slews to given equatorial coordinates (HA, Dec) in the mounts native epoch, <see cref="EquatorialSystem"/>.
    /// Uses current <see cref="SiderealTime"/> to convert to RA.
    /// Succeeds if <see cref="Connected"/> and <see cref="SlewRaDecAsync(double, double)"/> succeeds.
    /// </summary>
    /// <param name="ra">HA in hours (-12..12), as returned by <see cref="HourAngle"/></param>
    /// <param name="dec">Declination in degrees (-90..90)</param>
    /// <returns>True if slewing operation was accepted and mount is slewing</returns>
    bool SlewHourAngleDecAsync(double ha, double dec)
        => Connected
        && !double.IsNaN(SiderealTime)
        && ha is >= -12 and <= 12
        && SlewRaDecAsync(ConditionRA(SiderealTime - (ha + 12)), dec);

    /// <summary>
    /// The UTC date/time of the telescope's internal clock.
    /// Must be initalised from system time if no internal clock is supported.
    /// </summary>
    DateTime? UTCDate { get; set; }

    /// <summary>
    /// Returns true iff <see cref="UTCDate"/> was updated succcessfully when setting,
    /// typically via <code>UTCDate = DateTime.UTCNow</code>.
    /// </summary>
    bool TimeSuccessfullySynchronised { get; }

    bool TryGetUTCDate(out DateTime dateTime)
    {
        if (Connected && UTCDate is DateTime utc)
        {
            dateTime = utc;
            return true;
        }

        dateTime = DateTime.MinValue;
        return false;
    }

    PierSide SideOfPier { get; set; }

    /// <summary>
    /// Predict side of pier for German equatorial mounts.
    /// </summary>
    /// <param name="ra">The destination right ascension(hours)</param>
    /// <param name="dec">The destination declination (degrees, positive North)</param>
    /// <returns></returns>
    PierSide DestinationSideOfPier(double ra, double dec);

    /// <summary>
    /// Uses <see cref="DestinationSideOfPier"/> and equatorial coordinates as of now (<see cref="RightAscension"/>, <see cref="Declination"/>)
    /// To calculate the <see cref="SideOfPier"/> that the telescope should be on if one where to slew there now.
    /// </summary>
    PierSide ExpectedSideOfPier => Connected ? DestinationSideOfPier(RightAscension, Declination) : PierSide.Unknown;

    /// <summary>
    /// The current hour angle, using <see cref="RightAscension"/> and <see cref="SiderealTime"/>, (-12,12).
    /// </summary>
    double HourAngle => Connected ? ConditionHA(SiderealTime - RightAscension) : double.NaN;

    /// <summary>
    /// The local apparent sidereal time from the telescope's internal clock (hours, sidereal).
    /// </summary>
    double SiderealTime { get; }

    /// <summary>
    /// The right ascension (hours) of the telescope's current equatorial coordinates, in the coordinate system given by the <see cref="EquatorialSystem"/> property.
    /// </summary>
    double RightAscension { get; }

    /// <summary>
    /// The declination (degrees) of the telescope's current equatorial coordinates, in the coordinate system given by the <see cref="EquatorialSystem"/> property.
    /// </summary>
    double Declination { get; }

    /// <summary>
    /// The elevation above mean sea level (meters) of the site at which the telescope is located.
    /// </summary>
    double SiteElevation { get; set; }

    /// <summary>
    /// The geodetic(map) latitude (degrees, positive North, WGS84) of the site at which the telescope is located.
    /// </summary>
    double SiteLatitude { get; set; }

    /// <summary>
    /// The longitude (degrees, positive East, WGS84) of the site at which the telescope is located.
    /// </summary>
    double SiteLongitude { get; set; }

    /// <summary>
    /// Initialises using standard pressure and atmosphere. Please adjust if available.
    /// </summary>
    /// <param name="utcOffset">timezone offset</param>
    /// <param name="transform"></param>
    /// <returns></returns>
    bool TryGetTransform([NotNullWhen(true)] out Transform? transform)
    {
        if (Connected && TryGetUTCDate(out var utc))
        {
            transform = new Transform
            {
                SiteElevation = SiteElevation,
                SiteLatitude = SiteLatitude,
                SiteLongitude = SiteLongitude,
                SitePressure = 1010, // TODO standard atmosphere
                SiteTemperature = 10, // TODO check either online or if compatible devices connected
                DateTimeOffset = utc,
                Refraction = true // TODO assumes that driver does not support/do refraction
            };

            return true;
        }

        transform = null;
        return false;
    }
}