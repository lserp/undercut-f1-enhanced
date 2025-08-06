using UndercutF1.Console.Models;
using UndercutF1.Console.Services;
using UndercutF1.Data;

namespace UndercutF1.Data.Tests;

public class CircularTrackPositionCalculatorTests
{
    private readonly CircularTrackPositionCalculator _calculator = new();

    [Fact]
    public void GetTrackProgress_NoSectorData_ReturnsStartOfLap()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            Sectors = new Dictionary<string, TimingDataPoint.Driver.LapSectorTime>()
        };

        // Act
        var progress = _calculator.GetTrackProgress(driver);

        // Assert
        Assert.Equal(0.05, progress);
    }

    [Fact]
    public void GetTrackProgress_Sector1Complete_ReturnsOneThirdProgress()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            Sectors = new Dictionary<string, TimingDataPoint.Driver.LapSectorTime>
            {
                ["0"] = new() { Value = "25.123" }, // Sector 1 complete
                ["1"] = new() { Value = null },      // Sector 2 not complete
                ["2"] = new() { Value = null }       // Sector 3 not complete
            }
        };

        // Act
        var progress = _calculator.GetTrackProgress(driver);

        // Assert
        Assert.Equal(0.33, progress);
    }

    [Fact]
    public void GetTrackProgress_Sector2Complete_ReturnsTwoThirdsProgress()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            Sectors = new Dictionary<string, TimingDataPoint.Driver.LapSectorTime>
            {
                ["0"] = new() { Value = "25.123" }, // Sector 1 complete
                ["1"] = new() { Value = "28.456" }, // Sector 2 complete
                ["2"] = new() { Value = null }      // Sector 3 not complete
            }
        };

        // Act
        var progress = _calculator.GetTrackProgress(driver);

        // Assert
        Assert.Equal(0.66, progress);
    }

    [Fact]
    public void GetTrackProgress_AllSectorsComplete_ReturnsNearComplete()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            Sectors = new Dictionary<string, TimingDataPoint.Driver.LapSectorTime>
            {
                ["0"] = new() { Value = "25.123" }, // Sector 1 complete
                ["1"] = new() { Value = "28.456" }, // Sector 2 complete
                ["2"] = new() { Value = "24.789" }  // Sector 3 complete
            }
        };

        // Act
        var progress = _calculator.GetTrackProgress(driver);

        // Assert
        Assert.Equal(0.95, progress);
    }

    [Fact]
    public void CalculatePosition_ValidDriver_ReturnsCorrectPosition()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            NumberOfLaps = 25,
            Sectors = new Dictionary<string, TimingDataPoint.Driver.LapSectorTime>
            {
                ["0"] = new() { Value = "25.123" }, // Sector 1 complete
                ["1"] = new() { Value = null },      // Sector 2 not complete
                ["2"] = new() { Value = null }       // Sector 3 not complete
            }
        };

        // Act
        var position = _calculator.CalculatePosition(driver, 25, 25);

        // Assert
        Assert.Equal(25, position.LapNumber);
        Assert.Equal(0.33, position.TrackProgress);
        Assert.Equal(118.8, position.Angle, 1); // 0.33 * 360 = 118.8
        Assert.Equal(1.0, position.RadialPosition);
    }

    [Fact]
    public void CalculatePosition_DriverOneLapBehind_ReturnsCorrectRadialPosition()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            NumberOfLaps = 24,
            Sectors = new Dictionary<string, TimingDataPoint.Driver.LapSectorTime>()
        };

        // Act
        var position = _calculator.CalculatePosition(driver, 24, 25);

        // Assert
        Assert.Equal(0.9, position.RadialPosition);
    }

    [Fact]
    public void GetDriverStatus_RetiredDriver_ReturnsRetired()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            Retired = true
        };

        // Act
        var status = _calculator.GetDriverStatus(driver);

        // Assert
        Assert.Equal(DriverStatus.Retired, status);
    }

    [Fact]
    public void GetDriverStatus_InPitDriver_ReturnsInPit()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            InPit = true
        };

        // Act
        var status = _calculator.GetDriverStatus(driver);

        // Assert
        Assert.Equal(DriverStatus.InPit, status);
    }

    [Fact]
    public void GetDriverStatus_PitOutDriver_ReturnsPitOut()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            PitOut = true
        };

        // Act
        var status = _calculator.GetDriverStatus(driver);

        // Assert
        Assert.Equal(DriverStatus.PitOut, status);
    }

    [Fact]
    public void GetDriverStatus_OnTrackDriver_ReturnsOnTrack()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            InPit = false,
            PitOut = false,
            Retired = false,
            Stopped = false
        };

        // Act
        var status = _calculator.GetDriverStatus(driver);

        // Assert
        Assert.Equal(DriverStatus.OnTrack, status);
    }

    [Theory]
    [InlineData(0.05, 18.0)]   // Start of lap -> 0.05 * 360 = 18°
    [InlineData(0.33, 118.8)]  // Sector 1 complete -> 0.33 * 360 = 118.8°
    [InlineData(0.66, 237.6)]  // Sector 2 complete -> 0.66 * 360 = 237.6°
    [InlineData(0.95, 342.0)]  // All sectors complete -> 0.95 * 360 = 342°
    public void CalculatePosition_VariousSectorProgress_ReturnsCorrectAngle(double expectedProgress, double expectedAngle)
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            Sectors = new Dictionary<string, TimingDataPoint.Driver.LapSectorTime>()
        };

        // Set up sectors based on expected progress
        if (expectedProgress >= 0.33)
        {
            driver.Sectors["0"] = new() { Value = "25.123" };
        }
        if (expectedProgress >= 0.66)
        {
            driver.Sectors["1"] = new() { Value = "28.456" };
        }
        if (expectedProgress >= 0.95)
        {
            driver.Sectors["2"] = new() { Value = "24.789" };
        }

        // Act
        var position = _calculator.CalculatePosition(driver, 1, 1);

        // Assert
        Assert.Equal(expectedProgress, position.TrackProgress);
        Assert.Equal(expectedAngle, position.Angle, 1); // Allow 1 degree tolerance
    }

    [Fact]
    public void CalculatePosition_NullDriver_ReturnsDefaultPosition()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver();

        // Act
        var position = _calculator.CalculatePosition(driver, 1, 1);

        // Assert
        Assert.Equal(0.05, position.TrackProgress); // Default start of lap
        Assert.Equal(1, position.LapNumber);
        Assert.Equal(1.0, position.RadialPosition);
    }

    [Fact]
    public void GetTrackProgress_EmptyStringSectorValues_ReturnsStartOfLap()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            Sectors = new Dictionary<string, TimingDataPoint.Driver.LapSectorTime>
            {
                ["0"] = new() { Value = "" },
                ["1"] = new() { Value = "" },
                ["2"] = new() { Value = "" }
            }
        };

        // Act
        var progress = _calculator.GetTrackProgress(driver);

        // Assert
        Assert.Equal(0.05, progress);
    }

    [Fact]
    public void GetDriverStatus_StoppedDriver_ReturnsStopped()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            Stopped = true
        };

        // Act
        var status = _calculator.GetDriverStatus(driver);

        // Assert
        Assert.Equal(DriverStatus.Stopped, status);
    }

    [Fact]
    public void CalculatePosition_DriverMultipleLapsBehind_ReturnsCorrectRadialPosition()
    {
        // Arrange
        var driver = new TimingDataPoint.Driver
        {
            NumberOfLaps = 20,
            Sectors = new Dictionary<string, TimingDataPoint.Driver.LapSectorTime>()
        };

        // Act
        var position = _calculator.CalculatePosition(driver, 20, 25); // 5 laps behind

        // Assert
        Assert.Equal(0.6, position.RadialPosition); // More than 3 laps behind
    }}
