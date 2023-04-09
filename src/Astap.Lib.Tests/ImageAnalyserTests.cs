﻿using Astap.Lib.Astrometry.Focus;
using Astap.Lib.Devices;
using Astap.Lib.Imaging;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Astap.Lib.Tests;

public class ImageAnalyserTests
{
    const string PlateSolveTestFile = nameof(PlateSolveTestFile);
    const string PHD2SimGuider = nameof(PHD2SimGuider);

    private readonly ITestOutputHelper _testOutputHelper;

    private static readonly IReadOnlyDictionary<string, Image> _imageCache;

    static ImageAnalyserTests()
    {
        var imageCache = new Dictionary<string, Image>();
        foreach (var name in new[] {PlateSolveTestFile, PHD2SimGuider})
        {
            imageCache[name] = SharedTestData.ExtractGZippedFitsImage(name);
        }

        _imageCache = imageCache;
    }


    public ImageAnalyserTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData(PlateSolveTestFile, 10f)]
    [InlineData(PlateSolveTestFile, 15f)]
    public void GivenFileNameWhenWritingImageAndReadingBackThenItIsIdentical(string name, float snrMin)
    {
        // given
        var image = _imageCache[name];
        var fullPath = Path.Combine(Path.GetTempPath(), $"roundtrip_{Guid.NewGuid():D}.fits");
        IImageAnalyser imageAnalyser = new ImageAnalyser();
        var expectedStars = imageAnalyser.FindStars(image, snrMin: snrMin);

        try
        {
            // when
            image.WriteToFitsFile(fullPath);

            // then
            File.Exists(fullPath).ShouldBeTrue();
            Image.TryReadFitsFile(fullPath, out var readoutImage).ShouldBeTrue();
            readoutImage.Width.ShouldBe(image.Width);
            readoutImage.Height.ShouldBe(image.Height);
            readoutImage.BitDepth.ShouldBe(image.BitDepth);
            readoutImage.ImageMeta.Instrument.ShouldBe(image.ImageMeta.Instrument);
            readoutImage.MaxValue.ShouldBe(image.MaxValue);
            readoutImage.ImageMeta.ExposureStartTime.ShouldBe(image.ImageMeta.ExposureStartTime);
            readoutImage.ImageMeta.ExposureDuration.ShouldBe(image.ImageMeta.ExposureDuration);
            var starsFromImage = imageAnalyser.FindStars(image, snrMin: snrMin);

            starsFromImage.ShouldBeEquivalentTo(expectedStars);
        }
        finally
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }

    [Theory]
    [InlineData(PlateSolveTestFile)]
    public async Task GivenOnDiskFitsFileWithImageWhenTryingReadImageItSucceeds(string name)
    {
        // given
        var extractedFitsFile = await SharedTestData.ExtractGZippedFitsFileAsync(name);

        try
        {
            ImageDim dim;
            SharedTestData.TestFileImageDimAndCoords.TryGetValue(name, out var dimAndCoords).ShouldBeTrue();

            (dim, _, _) = dimAndCoords;

            // when
            var actualSuccess = Image.TryReadFitsFile(extractedFitsFile, out var image);

            // then
            image.ShouldNotBeNull();
            image.Width.ShouldBe(dim.Width);
            image.Height.ShouldBe(dim.Height);
            actualSuccess.ShouldBeTrue();
        }
        finally
        {
            File.Delete(extractedFitsFile);
        }
    }

    [Theory]
    [InlineData("image_file-snr-20_stars-28_1280x960x16", 10f, 89)]
    [InlineData("image_file-snr-20_stars-28_1280x960x16", 20f, 28)]
    [InlineData("image_file-snr-20_stars-28_1280x960x16", 30f, 13)]
    [InlineData("RGGB_frame_bx0_by0_top_down", 30f, 2722, 5000)]
    [InlineData("RGGB_frame_bx0_by0_top_down", 10f, 2956, 5000)]
    public async Task GivenImageFileAndMinSNRWhenFindingStarsThenTheyAreFound(string name, float snrMin, int expectedStars, int? maxStars = null)
    {
        // given
        var extractedFitsFile = await SharedTestData.ExtractGZippedFitsFileAsync(name);
        IImageAnalyser imageAnalyser = new ImageAnalyser();
        try
        {
            // when
            Image.TryReadFitsFile(extractedFitsFile, out var image).ShouldBeTrue();
            var actualStars = imageAnalyser.FindStars(image, snrMin, maxStars ?? 500);

            // then
            actualStars.ShouldNotBeEmpty();
            actualStars.Count.ShouldBe(expectedStars);
        }
        finally
        {
            File.Delete(extractedFitsFile);
        }
    }

    [Theory]
    [InlineData(10, 22)]
    [InlineData(15, 6)]
    [InlineData(20, 3)]
    public async Task GivenCameraImageDataWhenConvertingToImageThenStarsCanBeFound(int snr_min, int expectedStars)
    {
        // given
        const int Width = 1280;
        const int Height = 960;
        const BitDepth BitDepth = BitDepth.Int16;
        const int BlackLevel = 1;
        var expTime = TimeSpan.FromSeconds(42);
        var fileName = $"image_data_snr-{snr_min}_stars-{expectedStars}";
        var imageData = await SharedTestData.ExtractGZippedImageData(fileName, Width, Height);
        var imageMeta = new ImageMeta(fileName, DateTime.UtcNow, expTime, "", 2.4f, 2.4f, 190, -1, Filter.None, 1, 1, float.NaN, SensorType.Monochrome, 0, 0, RowOrder.TopDown);

        // when
        var image = ICameraDriver.DataToImage(imageData, ImageSourceFormat.WidthXHeightLE, BitDepth, BlackLevel, imageMeta);
        var stars = image?.FindStars(snr_min: snr_min);

        // then
        image.ShouldNotBeNull();
        image.Height.ShouldBe(Height);
        image.Width.ShouldBe(Width);
        image.BitDepth.ShouldBe(BitDepth);
        stars.ShouldNotBeNull().Count.ShouldBe(expectedStars);
    }


    [Theory]
    [InlineData(PlateSolveTestFile, 5, 3, 11, 1242, 220, 38)]
    [InlineData(PlateSolveTestFile, 9.5, 3, 6, 1242, 220, 38)]
    [InlineData(PlateSolveTestFile, 20, 3, 2, 1242, 220, 38)]
    [InlineData(PlateSolveTestFile, 30, 3, 1, 1242, 220, 38)]
    [InlineData(PHD2SimGuider, 2, 3, 10)]
    [InlineData(PHD2SimGuider, 5, 3, 10)]
    [InlineData(PHD2SimGuider, 5, 10, 10)]
    [InlineData(PHD2SimGuider, 20, 3, 7)]
    [InlineData(PHD2SimGuider, 30, 3, 2)]
    [InlineData(PHD2SimGuider, 30, 10, 2)]
    public void GivenFitsFileWhenAnalysingThenMedianHFDAndFWHMIsCalculated(string name, float snr_min, int max_retries, int expected_stars, params int[] sampleStar)
    {
        var analyser = new ImageAnalyser();

        // when
        var result = analyser.FindStars(_imageCache[name], snrMin: snr_min, maxIterations: max_retries);

        // then
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(expected_stars);
        result.ShouldAllBe(p => p.SNR >= snr_min);

        if (sampleStar is { Length: 3 })
        {
            var x = sampleStar[0];
            var y = sampleStar[1];
            var snr = sampleStar[2];
            result.ShouldContain(p => p.XCentroid > x - 1 && p.XCentroid < x + 1 && p.YCentroid > y - 1 && p.YCentroid < y + 1 && p.SNR > snr);
        }
        else if (sampleStar is { Length: > 0 })
        {
            Assert.Fail($"Sample star needs to be exactly 3 elements (x, y, snr), but only {sampleStar.Length} where given.");
        }
    }

    [Theory]
    [InlineData(SampleKind.HFD, 28208, 28211, 1, 1, 1, 10f, 20, 2, 130)]
    [InlineData(SampleKind.HFD, 28227, 28231, 1, 1, 1, 10f, 20, 2, 140)]
    [InlineData(SampleKind.HFD, 28208, 28231, 1, 1, 1, 10f, 20, 2, 130, Skip = "Computationally expensive")]
    public void GivenFocusSamplesWhenSolvingAHyperboleIsFound(SampleKind kind, int focusStart, int focusEndIncl, int focusStepSize, int sampleCount, int filterNo, float snrMin, int maxIterations, int expectedSolutionAfterSteps, int expectedMinStarCount)
    {
        // given
        var sampleMap = new MetricSampleMap(kind);
        IImageAnalyser imageAnalyser = new ImageAnalyser();

        // when
        for (int fp = focusStart; fp <= focusEndIncl; fp += focusStepSize)
        {
            for (int cs = 1; cs <= sampleCount; cs++)
            {
                var image = SharedTestData.ExtractGZippedFitsImage($"fp{fp}-cs{cs}-ms{sampleCount}-fw{filterNo}");

                var stars = imageAnalyser.FindStars(image, snrMin: snrMin);
                var median = imageAnalyser.MedianStarProperty(stars, sampleMap.Kind);
                var (solution, minPos, maxPos) = imageAnalyser.SampleStarsAtFocusPosition(sampleMap, fp, median, stars.Count, maxFocusIterations: maxIterations);

                _testOutputHelper.WriteLine($"focuspos={fp} stars={stars.Count} median={median} solution={solution} minPos={minPos} maxPos={maxPos}");

                median.ShouldBeGreaterThan(1f);
                stars.Count.ShouldBeGreaterThan(expectedMinStarCount);

                if (fp - focusStart >= expectedSolutionAfterSteps)
                {
                    (_, _, _, double error, int iterations) = solution.ShouldNotBeNull();
                    var minPosD = (double)minPos.ShouldNotBeNull();
                    var maxPosD = (double)maxPos.ShouldNotBeNull();

                    maxPosD.ShouldBeGreaterThan(minPosD);
                    minPosD.ShouldBe(focusStart);
                    iterations.ShouldBeLessThanOrEqualTo(maxIterations);
                    error.ShouldBeLessThan(1);
                }
                else
                {
                    solution.ShouldBeNull();
                }
            }
        }
    }
}
