I dont # Kite Range Algo

A comprehensive C# application that connects to Zerodha Kite WebSocket feed and constructs Range bars using automated token generation with advanced date-based token validation and IST timezone support.

## Project Structure

```
customALgo/
├── Config/                     # Configuration files and classes
│   ├── broker_config.json      # Main configuration file (template)
│   └── BrokerConfiguration.cs  # Configuration management class
├── Authentication/             # Token automation and authentication
│   ├── AutomatedTokenCapture.cs    # HTTP-based login automation
│   ├── TokenConfiguration.cs      # Token configuration model
│   ├── ZerodhaTokenService.cs      # Token service implementation
│   └── BrokerTokenManager.cs      # High-level token management
├── WebSocket/                  # WebSocket connectivity
│   └── KiteWebSocketClient.cs  # Kite WebSocket client implementation
├── Models/                     # Data models and structures
│   └── TickData.cs             # Tick data and OHLC models
├── RangeBars/                  # Range bar construction logic
│   └── PRange.cs               # Original NinjaTrader P_Range implementation
├── Zerodha/                    # Zerodha-specific modules and services
│   ├── Authentication/      # Token automation and authentication
│   │   ├── AutomatedTokenCapture.cs    # HTTP-based login automation
│   │   ├── TokenConfiguration.cs      # Token configuration model
│   │   ├── ZerodhaTokenService.cs      # Token service implementation
│   │   └── BrokerTokenManager.cs      # High-level token management
│   ├── Instruments/         # Instruments data management
│   │   ├── InstrumentData.cs        # Instrument entity model
│   │   ├── InstrumentsDbContext.cs  # SQLite database context
│   │   └── InstrumentsService.cs    # Instruments fetching service
│   └── WebSocket/           # WebSocket connectivity
│       └── KiteWebSocketClient.cs  # Kite WebSocket client implementation
├── Demo/                       # Demo and testing applications
│   ├── CustomAlgoDemo.cs    # Complete demo application
│   ├── TokenTestOnly.cs     # Token validation and testing utility
│   ├── InstrumentsDemo.cs   # Instruments fetching and storage demo
│   └── KiteRangeAlgoDemo.cs # Range algo demo application
├── Utilities/                  # Utility classes and helpers
│   └── TimeHelper.cs        # IST timezone utilities and market time functions
├── Data/                       # SQLite database storage
│   └── instruments.db       # Instruments database (auto-created)
└── Logs/                       # Log files (created at runtime)
```

## Features

### ✅ Implemented
- **Complete Token Automation**: Zero manual intervention with TOTP 2FA
- **HTTP-Only Authentication**: No WebView2 dependencies
- **Date-Based Token Validation**: Automatic daily token refresh using IST timezone
- **Instruments Data Management**: Automated fetching and SQLite storage of NSE/BSE instruments
- **Smart Masters Refresh**: Daily instruments update with configuration tracking
- **Advanced Time Management**: Consistent IST timezone handling with market hours support
- **WebSocket Connectivity**: Real-time tick data from Kite Connect
- **Multi-Instrument Support**: Configurable subscriptions (LTP/Quote/Full)
- **Auto-Reconnection**: Handles connection drops with exponential backoff
- **SQLite Database**: Optimized storage with indexes for fast instrument lookups
- **Comprehensive Logging**: Production-ready monitoring and debugging
- **Token Caching**: Automatic refresh with expiration management
- **Security**: Masks sensitive data in logs and output
- **Configuration Management**: Source file synchronization and validation
- **Testing Framework**: Comprehensive validation tests for all components

### 🔄 In Progress
- Range bar construction logic porting
- Integration of tick data with range bar generation
- Real-time range bar output

## Quick Start

### 1. Configuration Setup
1. Copy `Config/broker_config.json` and update with your Zerodha credentials:
   ```json
   {
     "kite_credentials": {
       "api_key": "your_api_key",
       "api_secret": "your_api_secret", 
       "user_id": "your_user_id",
       "password": "your_password",
       "totp_secret": "your_totp_secret_from_qr_code",
       "local_port": 8001
     }
   }
   ```

### 2. Run Token Validation Test
```bash
# Test token automation and validation
dotnet run --project KiteRangeAlgo.csproj
# or specifically run token tests
dotnet run TokenTestOnly.cs
```

### 3. Run Instruments Demo
```bash
# Test instruments fetching and storage
dotnet run InstrumentsDemo.cs
```

### 4. Run Full Demo
```bash
# Complete broker login and WebSocket demo
dotnet run KiteRangeAlgoDemo.cs
```

### 5. Expected Output

#### Token Validation Test Output
```
🔑 Token Automation Test
========================================
🕐 Current IST Time: 2025-07-04 17:11:45 IST
📈 Market Status: Market CLOSED - Opens in 16h 3m

🧪 TokenConfiguration Validation Tests
========================================
Test 1: Valid TokenConfiguration
✅ IsTokenExpired: False
✅ Masked String: ApiKey: test...3456, UserId: TEST001, LocalPort: 8001, HasToken: True, TokenExpired: False
✅ PASSED - Valid configuration accepted

Test 2: Token Generated Today (Same Date)
✅ Token generated 2 hours ago (same day) - IsExpired: False
✅ PASSED - Token from same date should not be expired due to date

Test 3: Token Generated Yesterday (Different Date)
✅ Token generated yesterday - IsExpired: True
✅ PASSED - Token from different date should be expired

🎯 Test Results: 7/7 tests passed
🎉 All TokenConfiguration validation tests PASSED!

📍 Using config file: /full/path/to/Config/broker_config.json
✅ Configuration loaded: API Key: 6g79...dmr7, User ID: XO3253, Instruments: 2
✅ Access token obtained: Isxst2...ejwTn6
```

#### Instruments Demo Output
```
📊 Kite Instruments Demo
========================================
🕐 Current IST Time: 2025-07-04 17:15:30 IST
📈 Market Status: Market CLOSED - Opens in 16h 0m

📍 Using config file: /full/path/to/Config/broker_config.json
✅ Configuration loaded: API Key: 6g79...dmr7, User ID: XO3253, Instruments: 2
✅ Access token obtained: Isxst2...ejwTn6
✅ Instruments service initialized with configuration integration

🔍 Checking if data refresh is needed...
📅 Current masters time: null
📅 Is masters refreshed: false
✅ Needs refresh: true

📥 Fetching NSE instruments...
✅ Stored 2,500 NSE instruments
📥 Fetching BSE instruments...
✅ Stored 4,200 BSE instruments
✅ Configuration updated with latest masters time

📈 Database Statistics:
  NSE: 2,500 instruments
  BSE: 4,200 instruments
  Total: 6,700 instruments

🔍 Search Examples:
  Searching for 'RELIANCE':
    RELIANCE - Reliance Industries Limited [EQ] (Token: 738561)
    
  Searching for 'INFY':
    INFY - Infosys Limited [EQ] (Token: 408065)

🎯 Getting instrument by token 738561:
    Found: RELIANCE (NSE) - Reliance Industries Limited [EQ]
    Last Price: ₹2847.50
    Is Equity: true
    Is Derivative: false
```

#### Full Demo Output
```
🚀 Kite Range Algo - Broker Login and WebSocket Demo
============================================================
📋 Step 1: Loading broker configuration...
✅ Configuration loaded: API Key: your..., User ID: XO3253, Instruments: 2
🔐 Step 2: Initializing token manager...
✅ Token manager initialized
🔑 Step 3: Testing TOTP generation...
✅ TOTP generated successfully: 123456
🎫 Step 4: Obtaining access token...
✅ Access token obtained: abcd...xyz
🔗 Step 5: Initializing WebSocket client...
✅ WebSocket client initialized
📡 Step 6: Connecting to Kite WebSocket...
✅ WebSocket connected successfully
📊 Step 7: Receiving live tick data...
📈 Tick #10: RELIANCE: 2847.50 @ 14:23:45.123
```

## Recent Updates & Improvements

### 🚀 Latest Features (July 2025)

#### Date-Based Token Validation
- **Smart Daily Refresh**: Tokens automatically expire and regenerate if generated on a different date (using IST timezone)
- **Market-Aware Timing**: Integration with Indian market hours (9:15 AM - 3:30 PM IST)
- **Timezone Consistency**: All time operations now use IST via `TimeHelper.cs` utility class
- **Backward Compatibility**: Time-based expiration (6 hours) remains as fallback for same-day scenarios

#### Enhanced Configuration Management
- **Source File Synchronization**: Application now updates the source `Config/broker_config.json` file directly
- **Path Resolution**: Automatic path resolution from runtime directory to source directory
- **Real-time Verification**: Immediate verification of saved configuration data
- **Debug Transparency**: Detailed logging shows exact file paths and operations

#### Comprehensive Testing Framework
- **TokenTestOnly.cs**: Dedicated test utility with 7 comprehensive validation scenarios:
  1. Valid configuration validation
  2. Same-date token validation (should not expire)
  3. Different-date token validation (should expire)
  4. Time-based expiration (6+ hours)
  5. Invalid configuration handling (missing fields)
  6. Port validation (boundary testing)
  7. Token update functionality verification

#### Improved Error Handling
- **Null Reference Safety**: Enhanced null checking in `BrokerTokenManager`
- **Graceful Failures**: Better exception handling with detailed logging
- **Configuration Validation**: Comprehensive validation with specific error messages

### 🔧 Technical Improvements

#### TimeHelper Utility
```csharp
// Market status checking
TimeHelper.IsMarketOpen()          // Returns true if NSE is open
TimeHelper.GetMarketStatus()        // Human-readable market status
TimeHelper.NowIST                   // Current time in IST
TimeHelper.FormatIST(dateTime)      // Consistent IST formatting
```

#### Enhanced Token Configuration
```csharp
// Date-based validation
public bool IsTokenExpired()
{
    // Check date first (IST timezone)
    if (tokenGeneratedIST.Date != currentIST.Date)
        return true;
    
    // Then check time-based expiration
    return currentIST - tokenGeneratedIST > TokenExpirationTime;
}
```

#### Configuration File Management
- **Automatic Source Updates**: No more manual copying between development and runtime files
- **Real-time Persistence**: Token updates immediately reflected in source configuration
- **Cross-Platform Paths**: Robust path handling for different development environments

## Dependencies

- **System.Net.WebSockets**: WebSocket client functionality
- **Newtonsoft.Json**: JSON configuration parsing
- **log4net**: Comprehensive logging framework
- **System.Net.Http**: HTTP requests for authentication
- **System.Security.Cryptography**: TOTP generation
- **Microsoft.EntityFrameworkCore**: Object-relational mapping framework
- **Microsoft.EntityFrameworkCore.Sqlite**: SQLite database provider
- **Microsoft.EntityFrameworkCore.Design**: Database design-time tools

## Architecture

### Authentication Flow
1. Load credentials from JSON configuration
2. Generate TOTP code using provided secret
3. Perform HTTP-based login with username/password
4. Submit TOTP for 2FA authentication  
5. Extract and exchange request token for access token
6. Cache token with expiration management

### WebSocket Flow
1. Authenticate using cached/generated access token
2. Connect to `wss://ws.kite.trade` with credentials
3. Subscribe to configured instruments by mode
4. Parse incoming binary tick data
5. Convert to OHLC format for range bar processing
6. Handle reconnection and error scenarios

### Instruments Management Flow
1. Check `instrument_masters_time` in configuration using IST timezone
2. If null or different date, fetch fresh instruments from Kite Connect API
3. Download and decompress gzipped CSV data for NSE, BSE, MCX, NFO exchanges
4. Parse CSV data and store in SQLite database with optimized indexes
5. Update `instrument_masters_time` in configuration file
6. Provide fast search and lookup capabilities for trading operations

### Data Processing
1. Receive real-time tick data from WebSocket
2. Parse binary format to extract OHLC, volume, timestamps
3. Apply range bar construction logic (from P_Range.cs)
4. Output constructed range bars for trading decisions

## Configuration Options

### Instruments
```json
"instruments": [
  {
    "token": 408065,        // Instrument token from Kite
    "symbol": "RELIANCE",   // Symbol name for identification
    "mode": "ltp"           // Subscription mode: ltp/quote/full
  }
]
```

### WebSocket Settings
```json
"websocket_settings": {
  "endpoint": "wss://ws.kite.trade",
  "auto_reconnect": true,
  "reconnect_interval": 5000,
  "max_reconnect_attempts": 10,
  "ping_interval": 30000
}
```

### Token Management
```json
"token_settings": {
  "auto_refresh": true,      // Automatically refresh expired tokens
  "expiration_hours": 6,     // Token expiration time (fallback for same-day)
  "max_retries": 3,          // Maximum retry attempts
  "cache_tokens": true       // Cache tokens between sessions
}
```

### Enhanced Features
```json
"kite_credentials": {
  "access_token": "auto_generated_daily",           // Automatically updated
  "access_token_time": "2025-07-04T17:11:45",      // IST timestamp
  "instrument_masters_time": "2025-07-04T08:30:00" // Daily masters update timestamp
}
```

## Security Notes

- **Never commit credentials**: Keep `broker_config.json` out of version control
- **TOTP Secret**: Obtain from Zerodha 2FA QR code setup
- **API Credentials**: Generate from Kite Connect developer console
- **Local Port**: Ensure port 8001 (or configured) is available for OAuth redirect

## Development Status

| Component | Status | Description |
|-----------|--------|-------------|
| Configuration | ✅ Complete | JSON-based configuration with validation |
| Authentication | ✅ Complete | Automated login with TOTP 2FA |
| Token Management | ✅ Complete | Date-based validation with IST timezone |
| Instruments Service | ✅ Complete | Automated NSE/BSE instruments fetching |
| SQLite Database | ✅ Complete | Optimized storage with EF Core |
| Masters Refresh | ✅ Complete | Daily instruments update tracking |
| Time Utilities | ✅ Complete | Market hours and IST timezone handling |
| WebSocket Client | ✅ Complete | Real-time data feed with reconnection |
| Tick Data Models | ✅ Complete | Comprehensive data structures |
| Testing Framework | ✅ Complete | Comprehensive validation tests |
| Configuration Sync | ✅ Complete | Source file synchronization |
| Range Bar Logic | 🔄 In Progress | Porting from NinjaTrader P_Range |
| Integration | 🔄 In Progress | Connecting tick data to range bars |

## Next Steps

1. Port range bar construction logic from `RangeBars/PRange.cs`
2. Integrate real-time tick processing with range bar generation
3. Add range bar output and persistence
4. Implement trading strategy integration
5. Add performance monitoring and optimization

## Support

For issues and questions:
1. Check the logs in `Logs/kite_algo.log`
2. Verify configuration in `Config/broker_config.json`
3. Test individual components using the demo application
4. Review authentication flow for token-related issues