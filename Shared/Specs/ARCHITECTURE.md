# StreamVision - Universal Media Player Architecture

## Overview
StreamVision is a cross-platform universal media player designed for IPTV streams, supporting M3U/M3U8 playlists, Xtream Codes API, and Stalker Portal.

## Core Features

### 1. Source Management
- M3U/M3U8 playlist import (URL or local file)
- Xtream Codes API integration
- Stalker Portal (MAC Address) support
- Multi-playlist management (up to 10 providers)

### 2. Video Player Engine
- Codecs: H.264, H.265 (HEVC), AV1
- Adaptive buffering
- Fast channel switching (<0.5s)
- Multi-audio track support
- Subtitle support (embedded & external)

### 3. Advanced Features
- EPG (Electronic Program Guide) with XMLTV
- Catch-up TV / Replay (up to 7 days)
- PVR Recording
- Multi-View / PIP (2 or 4 streams)
- Favorites management
- Search & filtering
- Parental controls

## Data Models

### Playlist Source
```json
{
  "id": "uuid",
  "name": "string",
  "type": "m3u|xtream|stalker",
  "url": "string",
  "username": "string?",
  "password": "string?",
  "macAddress": "string?",
  "epgUrl": "string?",
  "lastSync": "datetime",
  "isActive": "boolean"
}
```

### Channel
```json
{
  "id": "uuid",
  "sourceId": "uuid",
  "name": "string",
  "logoUrl": "string?",
  "streamUrl": "string",
  "groupTitle": "string",
  "epgId": "string?",
  "isFavorite": "boolean",
  "catchupDays": "int",
  "order": "int"
}
```

### EPG Program
```json
{
  "channelId": "string",
  "title": "string",
  "description": "string?",
  "startTime": "datetime",
  "endTime": "datetime",
  "category": "string?",
  "iconUrl": "string?"
}
```

## Platform-Specific Implementations

### Windows (C# / WPF)
- Uses LibVLCSharp for video playback
- Modern Fluent Design UI
- System tray integration
- Hardware acceleration via DirectX

### macOS (Swift / SwiftUI)
- Uses AVFoundation/AVKit
- Native macOS design language
- Menu bar integration
- Touch Bar support

### iOS (Swift / SwiftUI)
- AVFoundation for playback
- iOS 15+ design patterns
- AirPlay support
- Background audio
- Picture-in-Picture

### Android (Kotlin / Jetpack Compose)
- ExoPlayer for video
- Material Design 3
- Chromecast support
- Background playback service
- Android TV support
