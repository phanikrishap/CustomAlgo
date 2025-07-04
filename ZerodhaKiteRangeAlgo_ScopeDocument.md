# Zerodha Kite Range Algo - Scope Document

## Overview
Custom algorithm to connect to Zerodha Kite WebSocket feed and construct Range bars based on the P_Range.cs implementation.

## Key Components Analysis

### P_Range.cs Analysis
- **Core Logic**: Dynamic Range bar construction using `RecalculateRange()` method
- **Range Calculation**: Uses lookback bars (default 2) to calculate average range
- **Bar Formation**: New bars created when price moves beyond calculated range
- **Minimum Criteria**: Requires minimum ticks and time span before bar formation
- **OHLC Management**: Internal `OHLCBar` class for surrogate bar data

### Zerodha Kite WebSocket API
- **Endpoint**: `wss://ws.kite.trade`
- **Authentication**: API key + access token as query parameters
- **Subscription Modes**: 
  - `ltp` (8 bytes) - Last traded price only
  - `quote` (44 bytes) - Basic quote data
  - `full` (184 bytes) - Complete market depth
- **Data Format**: Binary for market quotes, text for orders/errors
- **Limits**: 3000 instruments per connection, 3 connections per API key

## Implementation Architecture

### 1. Configuration Management
```json
{
  "kite_credentials": {
    "api_key": "your_api_key",
    "api_secret": "your_api_secret",
    "user_id": "your_user_id", 
    "password": "your_password",
    "totp_secret": "your_totp_secret_from_qr_code",
    "local_port": 8001
  },
  "instruments": [
    {
      "token": 408065,
      "symbol": "RELIANCE",
      "mode": "ltp"
    }
  ],
  "range_settings": {
    "lookback_bars": 2,
    "recalc_bars": 2,
    "min_ticks": 1,
    "min_time_seconds": 1
  },
  "token_settings": {
    "auto_refresh": true,
    "expiration_hours": 6,
    "max_retries": 3
  }
}
```

### 2. Core Classes Structure
- **KiteWebSocketClient**: WebSocket connection management
- **RangeBarBuilder**: Port of P_Range logic for range bar construction
- **TickDataProcessor**: Process incoming tick data
- **ConfigurationManager**: JSON config handling
- **ZerodhaTokenService**: Automated token generation and management
- **AutomatedTokenCapture**: HTTP-based login automation with TOTP

### 3. Data Flow
1. Load credentials from JSON config
2. **Automated Login Process**:
   - Generate access token using existing token automation
   - Handle TOTP 2FA automatically
   - Cache token with expiration management
   - Auto-refresh when needed
3. Establish WebSocket connection to Kite with valid token
4. Subscribe to instrument tokens
5. Process incoming tick data
6. Apply range bar logic from P_Range.cs
7. Output constructed range bars

## Technical Requirements

### Dependencies
- WebSocket client library (e.g., WebSocketSharp)
- JSON parsing library (Newtonsoft.Json)
- **Existing Token Automation Components**:
  - AutomatedTokenCapture.cs
  - TokenConfiguration.cs
  - ZerodhaTokenService.cs
- log4net for logging
- System.Net.Http for HTTP requests
- System.Security.Cryptography for TOTP generation

### Key Methods to Port
- `RecalculateRange()` - Calculate dynamic range value
- `OnDataPoint()` - Process incoming tick data
- `CheckMinCriteria()` - Validate minimum requirements
- `OHLCBar` class - Internal bar representation

### Performance Considerations
- Efficient tick data processing
- Memory management for surrogate bars
- Real-time range recalculation

## Deliverables
1. **CustomAlgo.cs** - Main algorithm class
2. **kite_config.json** - Configuration file template
3. **KiteWebSocketClient.cs** - WebSocket connection handler
4. **RangeBarBuilder.cs** - Range bar construction logic
5. **TickData.cs** - Tick data model classes
6. **Integration of existing token automation**:
   - Copy AutomatedTokenCapture.cs
   - Copy TokenConfiguration.cs  
   - Copy ZerodhaTokenService.cs
7. **KiteTokenManager.cs** - Wrapper for token automation integration

## Success Criteria
- Successful WebSocket connection to Kite
- Real-time tick data reception
- Accurate range bar construction matching P_Range.cs logic
- Configurable parameters via JSON
- Proper error handling and reconnection logic

## Risks & Mitigation
- **API Rate Limits**: Implement proper throttling
- **Connection Drops**: Auto-reconnection with exponential backoff
- **Data Integrity**: Validate tick data before processing
- **Memory Usage**: Efficient management of historical data

## Automated Login Integration Benefits
- **Zero Manual Intervention**: Complete automation including TOTP 2FA
- **HTTP-Only Implementation**: No WebView2 dependencies
- **Production Ready**: Comprehensive error handling and logging
- **Token Caching**: Automatic token refresh with expiration management
- **Fallback Mechanisms**: Browser fallback if HTTP automation fails
- **Security**: Masks sensitive data in logs

## Timeline Estimation
- Token automation integration: 1-2 hours
- Configuration & WebSocket setup: 2-3 hours
- Range bar logic porting: 3-4 hours
- Integration & testing: 2-3 hours
- Total: 8-12 hours development time