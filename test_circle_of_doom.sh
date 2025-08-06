#!/bin/bash

echo "Testing Circle of Doom feature..."

# Check if sample data exists
if [ ! -d "$HOME/.local/share/undercut-f1/data/2024_Silverstone_Race" ]; then
    echo "Copying sample data..."
    mkdir -p "$HOME/.local/share/undercut-f1/data"
    cp -r "Sample Data/2024_Silverstone_Race" "$HOME/.local/share/undercut-f1/data/"
fi

echo "Sample data available:"
ls -la "$HOME/.local/share/undercut-f1/data/"

echo ""
echo "Starting undercutf1..."
echo "Instructions:"
echo "1. Press 'S' to go to Session screen"
echo "2. Press 'F' to start simulation"
echo "3. Select '2024_Silverstone_Race' and press Enter"
echo "4. Press 'C' to access Circle of Doom"
echo "5. Use Up/Down arrows to select drivers"
echo "6. Press Ctrl+C to exit"
echo ""
echo "Starting in 3 seconds..."
sleep 3

# Run the application
dotnet run --project UndercutF1.Console/UndercutF1.Console.csproj