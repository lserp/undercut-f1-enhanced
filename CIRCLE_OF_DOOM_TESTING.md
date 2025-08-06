# Circle of Doom - Testing Guide

## Quick Test

1. **Run the test script:**
   ```bash
   ./test_circle_of_doom.sh
   ```

2. **Manual testing steps:**
   ```bash
   # Start the application
   dotnet run --project UndercutF1.Console/UndercutF1.Console.csproj
   
   # In the application:
   # 1. Press 'S' → Session screen
   # 2. Press 'F' → Start simulation  
   # 3. Select '2024_Silverstone_Race' → Press Enter
   # 4. Press 'C' → Circle of Doom screen
   # 5. Use ▲/▼ → Select different drivers
   # 6. Observe pit strategy projections
   ```

## What You Should See

### With Graphics Support (Kitty/iTerm2/Sixel terminals):
- Circular visualization with driver dots positioned around the circle
- Ghost dots showing projected post-pit positions
- Dashed lines connecting current and projected positions
- Team colors for easy identification
- Interactive driver selection panel

### Without Graphics Support (most terminals):
- Message: "Terminal graphics not supported. Circle of Doom requires a compatible terminal."
- Driver selection panel still works
- Pit strategy calculations still displayed in text format

## Key Test Points

1. **Navigation**: Hotkey 'C' should switch to Circle of Doom from timing screens
2. **Driver Selection**: ▲/▼ arrows should change selected driver
3. **Pit Strategy Data**: Should show estimated pit time loss for current track
4. **Data Integration**: Should use live timing data when available
5. **Fallback Behavior**: Should gracefully handle missing data

## Pit Stop Time Examples

- **Silverstone**: ~24.0s total time loss
- **Monaco**: ~22.5s total time loss  
- **Spa-Francorchamps**: ~29.0s total time loss
- **Monza**: ~27.0s total time loss

## Troubleshooting

- **Application won't start**: Check that sample data is copied to `~/.local/share/undercut-f1/data/`
- **Circle of Doom not accessible**: Make sure you're in a timing screen before pressing 'C'
- **No graphics**: This is expected in most terminals - the feature still works in text mode
- **No pit projections**: This is normal if no live timing data is available

## Next Steps for Enhancement

1. **Sixel Graphics Support**: Add PNG to Sixel conversion for wider terminal support
2. **Dynamic Lap Time Calculation**: Use actual lap times instead of fixed 90s average
3. **Undercut/Overcut Analysis**: Add specific pit window calculations
4. **Multiple Pit Stop Scenarios**: Show different tire strategy options
5. **Real-time Updates**: Smooth animation of driver positions during live sessions