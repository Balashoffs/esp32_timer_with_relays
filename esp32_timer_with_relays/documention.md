# ESP32 Timer with Relays - Technical Documentation

## Overview

This is a nanoFramework-based embedded system application for ESP32 that controls a heating device through relays. The system provides multiple timed heating programs selectable via analog buttons, with visual feedback through LEDs.

## System Architecture

### Hardware Configuration

#### GPIO Pins (Output)
- **GPIO 25** - Green "Ready" LED (indicates system status)
- **GPIO 26** - Red "Job" LED (indicates relay/heating status)
- **GPIO 16** - Relay control pin (controls heating element)

#### ADC Channels (Input)
- **Channel 4** - Program buttons 1, 2, and 3
- **Channel 5** - Program button 4 and Reset button

#### Button Configuration

| Button | Channel | Function | ADC Range (min-max) |
|--------|---------|----------|---------------------|
| Program 1 | 4 | 8 hours/minutes | 1900-2000 |
| Program 2 | 4 | 6 hours/minutes | 2600-2700 |
| Program 3 | 4 | 3250-3350 | 4 hours/minutes |
| Program 4 | 5 | 2 hours/minutes | 1900-2000 |
| Reset | 5 | Stop operation | 2600-2700 |

## Core Components

### 1. **Program (Main Entry Point)**

The main class that initializes and orchestrates all system components.

**Key Responsibilities:**
- Initializes GPIO controller and ADC controller
- Creates instances of all service classes
- Wires up event handlers between services
- Manages the main application loop

**Event Flow:**
```
ADC Button Press → AdcService → Program → HeatService → Relay/LEDs
```

### 2. **Timing System**

#### ITiming Interface
Defines the contract for timing configurations with properties for:
- Heating duration
- Cooling duration
- Four program durations

#### DevTiming Class (Development Mode)
Fast timing for testing:
- **Heating:** 4 seconds
- **Cooling:** 1 second
- **Programs:** 30s, 60s, 90s, 120s

#### ProdTiming Class (Production Mode)
Real-world timing:
- **Heating:** 4 minutes
- **Cooling:** 1 minute
- **Programs:** 8h, 6h, 4h, 2h

#### TimingService Class
Converts TimeSpan values to microseconds (μs) for use with high-resolution timers.

**Conversion Formula:** `microseconds = ticks / 10`

### 3. **HeatService**

Central service managing the heating cycle logic.

**State Machine:**
```
[Idle] → [Heating] → [Cooling] → [Heating] → ... → [Stop]
```

**Components:**
- `_heatingTimer`: Controls heating phase duration
- `_coolingTimer`: Controls cooling phase duration
- `_jobTimer`: Controls total program duration

**Methods:**
- `ExecuteHeating(ulong delay)`: Starts a heating program with specified total duration
- `Reset()`: Stops all timers and returns to idle state
- `Dispose()`: Cleanup method for timer resources

**Events:**
- `HeatingActionEventHandler`: Notifies listeners of heating state changes (Heating, Cooling, Stop)

**Operation Flow:**
1. User selects program → `ExecuteHeating()` called
2. Job timer starts for total duration
3. Heating timer starts → Relay ON
4. Heating timer expires → Cooling timer starts → Relay OFF
5. Cooling timer expires → Heating timer starts → Relay ON
6. Cycle repeats until job timer expires or reset is pressed

### 4. **LED Information System**

#### LedInformationService
Coordinates both LED controllers to provide visual feedback.

**States:**
- **Default:** Green LED solid ON (waiting for selection)
- **Heating:** Green LED blinking + Red LED blinking (relay ON)
- **Cooling:** Green LED blinking + Red LED OFF (relay OFF)

#### LedController (Abstract Base)
Base class providing:
- High-resolution timer for LED blinking
- GPIO control
- Common blinking logic (500ms period)

#### HeatingLedController
Controls the red LED:
- Starts solid HIGH
- Blinks at 500ms intervals when heating is active
- Returns to solid HIGH when stopped

#### JobLedController
Controls the green LED:
- **Waiting():** Solid LOW (system ready)
- **Running():** Blinking at 500ms intervals

### 5. **ADC Button System**

#### AdcService
Scans analog inputs to detect button presses.

**Operation:**
- Polls ADC channels every 250ms
- Compares readings against button thresholds
- Fires `OnPressButonEventHandler` when button detected

**Debouncing:** Implicit through 250ms polling interval

#### AnalogReader
Wrapper for ADC channel operations:
- Opens and manages ADC channel
- Reads raw ADC values
- Provides debug output for values > 100

#### AnalogButton
Represents a single button with:
- Button type identifier
- Min/max ADC value range
- `IsThisBtn(int value)`: Range checking method

**Tolerance Design:**
Each button has ±100 ADC units tolerance to account for:
- Component variations
- Temperature drift
- Voltage fluctuations

### 6. **GPIO Management**

#### CustomOutputGpio
Simplified GPIO output wrapper:
- Opens pin in output mode
- Provides `WritePin()` method for setting HIGH/LOW

**Pin State:**
- `PinValue.High`: Typically means OFF for LEDs (inverted logic)
- `PinValue.Low`: Typically means ON for LEDs

## Data Flow Diagrams

### Button Press Flow
```
1. User presses button
2. ADC voltage changes
3. AdcService timer reads value (every 250ms)
4. AnalogButton determines if in range
5. OnPressButonEventHandler fires
6. Program.Main evaluates button type
7. HeatService.ExecuteHeating() called
8. Heating cycle begins
```

### Heating Cycle Flow
```
1. ExecuteHeating(delay) called
2. Job timer starts for total duration
3. Heating timer starts
4. HeatingActionEventHandler fires (Heating)
5. Relay turns ON, LEDs update
6. Heating timer expires
7. Cooling timer starts
8. HeatingActionEventHandler fires (Cooling)
9. Relay turns OFF, LEDs update
10. Cooling timer expires
11. Return to step 3 (repeat)
12. Job timer expires OR reset pressed
13. HeatingActionEventHandler fires (Stop)
14. System returns to idle
```

## Enumerations

### ButtonType
Identifies which button was pressed:
- `Program1` through `Program4`: Heating programs
- `Reset`: Emergency stop/cancel

### DevelopStage
Determines timing configuration:
- `Develop`: Fast timing for testing
- `Production`: Real-world timing

### HeatingStatus
Current state of heating system:
- `Heating`: Relay ON, actively heating
- `Cooling`: Relay OFF, cooling phase
- `Stop`: System idle

## Event System

### HeatingActionEventArgs
Carries heating status information between HeatService and other components.

### AnalogButtonEventArgs
Carries button type information from AdcService to main program.

## Utility Classes

### Utils
Static helper class:
- `ConvertFromTsToUs(TimeSpan)`: Converts TimeSpan to microseconds

**Note:** Microseconds are used for high-precision timing with HighResTimer.

## Configuration and Deployment

### Build Modes
Switch between development and production by changing:
```csharp
ITiming.BuildOn(DevelopStage.Production)  // or DevelopStage.Develop
```

### Flash Commands
```bash
# List available ports
nanoff --listports

# Get device details
nanoff --platform ESP32 --serialport [COM_PORT] --devicedetails

# Flash firmware with mass erase
nanoff --update --target ESP32_REV3 --serialport [COM_PORT] --masserase true --reset
```

## Safety Features

1. **Reset Button:** Immediate stop of all operations
2. **Debouncing:** 250ms polling prevents multiple triggers
3. **State Management:** Clean state transitions prevent race conditions
4. **Timer Cleanup:** Proper disposal of resources
5. **ADC Range Checking:** Prevents false triggers from noise

## Memory and Performance

- **Polling Interval:** 250ms for ADC scanning (minimal CPU usage)
- **LED Update:** 500ms blink cycle
- **Timer Resolution:** Microsecond precision
- **Blocking:** Main thread sleeps indefinitely; all work done by timers

## Limitations and Considerations

1. **No Persistent Storage:** Settings lost on power cycle
2. **Single Program:** Cannot queue multiple programs
3. **No Temperature Feedback:** Open-loop control only
4. **Hard-coded Thresholds:** ADC values may need calibration
5. **No Power Failure Recovery:** System resets to idle on restart

## Extension Points

The architecture supports easy extension:
- Add more buttons by expanding ADC channels
- Implement different heating patterns by modifying HeatService
- Add temperature sensing through additional ADC channels
- Implement persistence using ESP32 flash storage
- Add WiFi/Bluetooth for remote control

## LED Logic Summary

| System State | Green LED | Red LED | Relay |
|--------------|-----------|---------|-------|
| Waiting | Solid ON | OFF | OFF |
| Heating | Blinking | Blinking | ON |
| Cooling | Blinking | OFF | OFF |
| Stopped | Solid ON | OFF | OFF |

This documentation provides a complete understanding of the system's architecture, operation, and design decisions.
