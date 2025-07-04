# Kite Range Algo

A comprehensive C# application that connects to Zerodha Kite WebSocket feed and constructs Range bars using automated token generation.

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
├── Demo/                       # Demo and testing applications
│   └── CustomAlgoDemo.cs    # Complete demo application
└── Logs/                       # Log files (created at runtime)
```

## Features

### ✅ Implemented
- **Complete Token Automation**: Zero manual intervention with TOTP 2FA
- **HTTP-Only Authentication**: No WebView2 dependencies
- **WebSocket Connectivity**: Real-time tick data from Kite Connect
- **Multi-Instrument Support**: Configurable subscriptions (LTP/Quote/Full)
- **Auto-Reconnection**: Handles connection drops with exponential backoff
- **Comprehensive Logging**: Production-ready monitoring and debugging
- **Token Caching**: Automatic refresh with expiration management
- **Security**: Masks sensitive data in logs and output

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

### 2. Run Demo
```bash
cd Demo
dotnet run CustomAlgoDemo.cs
```

### 3. Expected Output
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

## Dependencies

- **System.Net.WebSockets**: WebSocket client functionality
- **Newtonsoft.Json**: JSON configuration parsing
- **log4net**: Comprehensive logging framework
- **System.Net.Http**: HTTP requests for authentication
- **System.Security.Cryptography**: TOTP generation

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
  "expiration_hours": 6,     // Token expiration time
  "max_retries": 3,          // Maximum retry attempts
  "cache_tokens": true       // Cache tokens between sessions
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
| WebSocket Client | ✅ Complete | Real-time data feed with reconnection |
| Tick Data Models | ✅ Complete | Comprehensive data structures |
| Range Bar Logic | 🔄 In Progress | Porting from NinjaTrader P_Range |
| Integration | 🔄 In Progress | Connecting tick data to range bars |
| Testing | ✅ Complete | Demo application with full workflow |

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