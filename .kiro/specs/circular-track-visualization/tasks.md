# Implementation Plan

- [x] 1. Set up core data models and enums
  - Create CircularPosition record with angle, radial position, lap number, and track progress properties
  - Create DriverPosition record with driver info, position, status, and gap data
  - Add CircularTrack enum value to Screen.cs
  - _Requirements: 1.1, 1.2, 1.3_

- [x] 2. Implement position calculation logic
  - Create CircularTrackPositionCalculator class with methods to convert timing data to circular positions
  - Implement sector-based interpolation logic for intra-lap progress calculation
  - Add multi-lap handling for drivers on different laps
  - Write unit tests for position calculation accuracy and edge cases
  - _Requirements: 2.1, 2.2, 2.3, 2.4_

- [x] 3. Create ASCII circle rendering system
  - Implement CircularTrackRenderer class with methods to generate ASCII-based circular visualization
  - Add mathematical circle point calculation using terminal character grid
  - Implement driver dot positioning with collision avoidance for nearby drivers
  - Create dynamic scaling logic to adjust circle size based on terminal dimensions
  - _Requirements: 1.1, 1.2, 3.1, 3.3_

- [x] 4. Implement main display class
  - Create CircularTrackDisplay class implementing IDisplay interface
  - Integrate with existing data processors (TimingDataProcessor, DriverListProcessor, etc.)
  - Implement GetContentAsync method to coordinate data and render visualization
  - Add real-time update handling with appropriate refresh rates
  - _Requirements: 1.1, 1.2, 2.1, 4.1, 4.3_

- [x] 5. Add driver identification and team colors
  - Implement team color application using ANSI color codes
  - Add driver number/TLA display on or near driver dots
  - Create visual separation logic to prevent driver overlap
  - Ensure consistent team colors match existing application standards
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 6. Create input handler for navigation
  - Implement SwitchToCircularTrackInputHandler class extending IInputHandler
  - Add key binding (R key) for switching to circular track display
  - Register input handler in applicable screens array
  - Implement screen switching logic using existing State management
  - _Requirements: 4.1, 4.2, 4.3_

- [x] 7. Add session context and information display
  - Implement session information display (lap count, session type, timing)
  - Add driver status indicators for pit, out, retired states
  - Create lap completion visual indicators
  - Add data freshness indicators for stale timing data
  - _Requirements: 4.4, 5.1, 5.2, 5.3, 6.1, 6.2, 6.3, 6.4_

- [x] 8. Implement error handling and edge cases
  - Add handling for missing or incomplete timing data
  - Implement fallback displays for no driver data scenarios
  - Add terminal compatibility checks and fallbacks
  - Create graceful handling of session transitions and data gaps
  - _Requirements: 5.4, 6.4_

- [x] 9. Add comprehensive unit tests
  - Write tests for CircularTrackPositionCalculator with various timing scenarios
  - Create tests for CircularTrackRenderer ASCII generation and scaling
  - Add integration tests for data processor interactions
  - Test edge cases like single driver, all retired, session restarts
  - _Requirements: 1.4, 2.1, 2.2, 2.3, 2.4, 5.1, 5.2, 5.3, 5.4_

- [x] 10. Integrate with existing application architecture
  - Register CircularTrackDisplay in dependency injection container
  - Add input handler to ConsoleLoop input processing
  - Ensure proper screen navigation flow with existing displays
  - Test integration with live timing data and session management
  - _Requirements: 4.1, 4.2, 4.3, 4.4_