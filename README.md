# Unity OPC UA Client for CODESYS

A robust Unity client that communicates with CODESYS OPC UA servers to monitor and control industrial automation systems. Supports reading/writing tags, automatic reconnection, and thread-safe Unity integration.

## Key Features
- 📡 **OPC UA Communication**: Connects to CODESYS OPC UA servers (tested with Raspberry Pi)
- 🔄 **Bi-directional Data Flow**: Read and write tag values in real-time
- 🛡️ **Automatic Reconnection**: Self-healing connection with configurable retry
- 🧵 **Thread-Safe Updates**: Main-thread queue for Unity UI compatibility
- 📊 **Preconfigured Tags**: Position data, sensors, and control commands
- ⏱️ **Timestamp Tracking**: Records when values were last updated

## Code Architecture

### Core Components
| Class/Component       | Purpose |
|-----------------------|---------|
| `OPCUAClient`         | Main controller handling server connection and tag management |
| `TagValue`            | Serializable container for tag data (value, timestamp, type) |
| `MonitoredItem`       | OPC UA subscription items for specific node IDs |

### Key Methods
```csharp
// 1. Connection Management
StartAsync()          // Initializes OPC UA connection and subscriptions
ReconnectAsync()      // Automatic reconnection loop (5s intervals)

// 2. Data Flow
SetupSubscription()   // Creates subscriptions for all configured tags
OnMonitoredItemNotification() // Handles incoming tag updates
WriteTagValueAsync()  // Writes values back to the OPC UA server

// 3. Type Conversion
ConvertValueToExpectedType() // Ensures proper data type formatting
GetNodeIdByDisplayName()    // Maps tag names to OPC UA node IDs
