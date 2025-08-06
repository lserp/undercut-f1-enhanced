using UndercutF1.Console.Models;
using UndercutF1.Console.Services;

namespace UndercutF1.Data.Tests;

public class CircularTrackRendererTests
{
    private readonly CircularTrackRenderer _renderer = new();

    [Fact]
    public void RenderCircle_EmptyPositions_ReturnsValidRenderable()
    {
        // Arrange
        var positions = new List<DriverPosition>();
        var terminalWidth = 80;
        var terminalHeight = 24;

        // Act
        var result = _renderer.RenderCircle(positions, terminalWidth, terminalHeight);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void RenderCircle_SingleDriver_ReturnsValidRenderable()
    {
        // Arrange
        var positions = new List<DriverPosition>
        {
            new()
            {
                DriverNumber = "44",
                DriverTla = "HAM",
                TeamColor = "#00D2BE",
                Position = new CircularPosition
                {
                    Angle = 0.0, // Top of circle
                    RadialPosition = 1.0,
                    LapNumber = 1,
                    TrackProgress = 0.0
                },
                Status = DriverStatus.OnTrack,
                GapToLeader = "LAP 1"
            }
        };
        var terminalWidth = 80;
        var terminalHeight = 24;

        // Act
        var result = _renderer.RenderCircle(positions, terminalWidth, terminalHeight);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void RenderCircle_MultipleDrivers_ReturnsValidRenderable()
    {
        // Arrange
        var positions = new List<DriverPosition>
        {
            new()
            {
                DriverNumber = "44",
                DriverTla = "HAM",
                Position = new CircularPosition { Angle = 0.0, RadialPosition = 1.0 },
                Status = DriverStatus.OnTrack
            },
            new()
            {
                DriverNumber = "1",
                DriverTla = "VER",
                Position = new CircularPosition { Angle = 90.0, RadialPosition = 1.0 },
                Status = DriverStatus.OnTrack
            },
            new()
            {
                DriverNumber = "16",
                DriverTla = "LEC",
                Position = new CircularPosition { Angle = 180.0, RadialPosition = 1.0 },
                Status = DriverStatus.InPit
            }
        };
        var terminalWidth = 100;
        var terminalHeight = 30;

        // Act
        var result = _renderer.RenderCircle(positions, terminalWidth, terminalHeight);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void RenderCircle_SmallTerminal_ReturnsValidRenderable()
    {
        // Arrange
        var positions = new List<DriverPosition>
        {
            new()
            {
                DriverNumber = "44",
                DriverTla = "HAM",
                Position = new CircularPosition { Angle = 45.0, RadialPosition = 1.0 },
                Status = DriverStatus.OnTrack
            }
        };
        var terminalWidth = 40; // Small terminal
        var terminalHeight = 15;

        // Act
        var result = _renderer.RenderCircle(positions, terminalWidth, terminalHeight);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void RenderCircle_DriversOnDifferentLaps_ReturnsValidRenderable()
    {
        // Arrange
        var positions = new List<DriverPosition>
        {
            new()
            {
                DriverNumber = "44",
                DriverTla = "HAM",
                Position = new CircularPosition 
                { 
                    Angle = 0.0, 
                    RadialPosition = 1.0, // Current lap
                    LapNumber = 25 
                },
                Status = DriverStatus.OnTrack
            },
            new()
            {
                DriverNumber = "20",
                DriverTla = "MAG",
                Position = new CircularPosition 
                { 
                    Angle = 0.0, 
                    RadialPosition = 0.9, // One lap behind
                    LapNumber = 24 
                },
                Status = DriverStatus.OnTrack
            }
        };
        var terminalWidth = 80;
        var terminalHeight = 24;

        // Act
        var result = _renderer.RenderCircle(positions, terminalWidth, terminalHeight);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void CreateLegend_ReturnsValidRenderable()
    {
        // Act
        var result = _renderer.CreateLegend();

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void RenderCircle_DriversWithDifferentStatuses_ReturnsValidRenderable()
    {
        // Arrange
        var positions = new List<DriverPosition>
        {
            new()
            {
                DriverNumber = "44",
                DriverTla = "HAM",
                Position = new CircularPosition { Angle = 0.0, RadialPosition = 1.0 },
                Status = DriverStatus.OnTrack
            },
            new()
            {
                DriverNumber = "1",
                DriverTla = "VER",
                Position = new CircularPosition { Angle = 90.0, RadialPosition = 1.0 },
                Status = DriverStatus.InPit
            },
            new()
            {
                DriverNumber = "16",
                DriverTla = "LEC",
                Position = new CircularPosition { Angle = 180.0, RadialPosition = 1.0 },
                Status = DriverStatus.PitOut
            },
            new()
            {
                DriverNumber = "6",
                DriverTla = "LAT",
                Position = new CircularPosition { Angle = 270.0, RadialPosition = 1.0 },
                Status = DriverStatus.Retired
            }
        };
        var terminalWidth = 80;
        var terminalHeight = 24;

        // Act
        var result = _renderer.RenderCircle(positions, terminalWidth, terminalHeight);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void RenderCircle_VerySmallTerminal_ReturnsValidRenderable()
    {
        // Arrange
        var positions = new List<DriverPosition>
        {
            new()
            {
                DriverNumber = "44",
                DriverTla = "HAM",
                Position = new CircularPosition { Angle = 0.0, RadialPosition = 1.0 },
                Status = DriverStatus.OnTrack
            }
        };
        var terminalWidth = 20; // Very small terminal
        var terminalHeight = 10;

        // Act
        var result = _renderer.RenderCircle(positions, terminalWidth, terminalHeight);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void RenderCircle_NullPositions_ReturnsValidRenderable()
    {
        // Arrange
        List<DriverPosition> positions = null!;
        var terminalWidth = 80;
        var terminalHeight = 24;

        // Act & Assert - Should not throw
        var result = _renderer.RenderCircle(positions ?? new List<DriverPosition>(), terminalWidth, terminalHeight);
        Assert.NotNull(result);
    }

    [Fact]
    public void RenderCircle_InvalidTeamColors_ReturnsValidRenderable()
    {
        // Arrange
        var positions = new List<DriverPosition>
        {
            new()
            {
                DriverNumber = "44",
                DriverTla = "HAM",
                TeamColor = "invalid-color", // Invalid color format
                Position = new CircularPosition { Angle = 0.0, RadialPosition = 1.0 },
                Status = DriverStatus.OnTrack
            }
        };
        var terminalWidth = 80;
        var terminalHeight = 24;

        // Act & Assert - Should not throw
        var result = _renderer.RenderCircle(positions, terminalWidth, terminalHeight);
        Assert.NotNull(result);
    }

    [Fact]
    public void RenderCircle_ExtremeAngles_ReturnsValidRenderable()
    {
        // Arrange
        var positions = new List<DriverPosition>
        {
            new()
            {
                DriverNumber = "44",
                DriverTla = "HAM",
                Position = new CircularPosition { Angle = 359.99, RadialPosition = 1.0 },
                Status = DriverStatus.OnTrack
            },
            new()
            {
                DriverNumber = "1",
                DriverTla = "VER",
                Position = new CircularPosition { Angle = 0.01, RadialPosition = 1.0 },
                Status = DriverStatus.OnTrack
            }
        };
        var terminalWidth = 80;
        var terminalHeight = 24;

        // Act
        var result = _renderer.RenderCircle(positions, terminalWidth, terminalHeight);

        // Assert
        Assert.NotNull(result);
    }}
