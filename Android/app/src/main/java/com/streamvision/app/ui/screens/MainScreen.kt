package com.streamvision.app.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import coil.compose.AsyncImage
import com.streamvision.app.data.models.*
import com.streamvision.app.ui.viewmodel.MainViewModel
import com.streamvision.app.ui.viewmodel.Tab

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MainScreen(
    viewModel: MainViewModel = hiltViewModel()
) {
    val uiState by viewModel.uiState.collectAsState()
    val playlistSources by viewModel.playlistSources.collectAsState()
    val channelGroups by viewModel.channelGroups.collectAsState()
    val favoriteChannels by viewModel.favoriteChannels.collectAsState()
    val recentChannels by viewModel.recentChannels.collectAsState()
    val searchQuery by viewModel.searchQuery.collectAsState()

    var selectedTab by remember { mutableStateOf(Tab.CHANNELS) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        when (selectedTab) {
                            Tab.CHANNELS -> "Channels"
                            Tab.FAVORITES -> "Favorites"
                            Tab.RECENT -> "Recent"
                            Tab.SETTINGS -> "Settings"
                        }
                    )
                },
                actions = {
                    if (selectedTab == Tab.CHANNELS) {
                        // Source selector
                        if (playlistSources.isNotEmpty()) {
                            var expanded by remember { mutableStateOf(false) }
                            Box {
                                TextButton(onClick = { expanded = true }) {
                                    Text(
                                        uiState.selectedSource?.name ?: "Select",
                                        maxLines = 1
                                    )
                                    Icon(Icons.Default.ArrowDropDown, null)
                                }
                                DropdownMenu(
                                    expanded = expanded,
                                    onDismissRequest = { expanded = false }
                                ) {
                                    playlistSources.forEach { source ->
                                        DropdownMenuItem(
                                            text = { Text(source.name) },
                                            onClick = {
                                                viewModel.selectSource(source)
                                                expanded = false
                                            },
                                            leadingIcon = {
                                                if (source.id == uiState.selectedSource?.id) {
                                                    Icon(Icons.Default.Check, null)
                                                }
                                            }
                                        )
                                    }
                                }
                            }
                        }

                        IconButton(onClick = { viewModel.showAddPlaylistDialog(true) }) {
                            Icon(Icons.Default.Add, "Add Playlist")
                        }
                    }
                }
            )
        },
        bottomBar = {
            Column {
                // Mini Player
                if (uiState.currentChannel != null) {
                    MiniPlayer(
                        channel = uiState.currentChannel!!,
                        isPlaying = uiState.isPlaying,
                        onPlayPause = { viewModel.togglePlayPause() },
                        onStop = { viewModel.stop() },
                        onClick = { viewModel.showFullScreenPlayer(true) }
                    )
                }

                // Bottom Navigation
                NavigationBar {
                    NavigationBarItem(
                        icon = { Icon(Icons.Default.Tv, null) },
                        label = { Text("Channels") },
                        selected = selectedTab == Tab.CHANNELS,
                        onClick = { selectedTab = Tab.CHANNELS }
                    )
                    NavigationBarItem(
                        icon = { Icon(Icons.Default.Star, null) },
                        label = { Text("Favorites") },
                        selected = selectedTab == Tab.FAVORITES,
                        onClick = { selectedTab = Tab.FAVORITES }
                    )
                    NavigationBarItem(
                        icon = { Icon(Icons.Default.History, null) },
                        label = { Text("Recent") },
                        selected = selectedTab == Tab.RECENT,
                        onClick = { selectedTab = Tab.RECENT }
                    )
                    NavigationBarItem(
                        icon = { Icon(Icons.Default.Settings, null) },
                        label = { Text("Settings") },
                        selected = selectedTab == Tab.SETTINGS,
                        onClick = { selectedTab = Tab.SETTINGS }
                    )
                }
            }
        }
    ) { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            when (selectedTab) {
                Tab.CHANNELS -> ChannelsTab(
                    channelGroups = channelGroups,
                    searchQuery = searchQuery,
                    currentChannel = uiState.currentChannel,
                    onSearchQueryChange = { viewModel.updateSearchQuery(it) },
                    onChannelClick = { viewModel.playChannel(it) },
                    onFavoriteClick = { viewModel.toggleFavorite(it) },
                    onRefresh = { viewModel.refreshPlaylist() },
                    isLoading = uiState.isLoading,
                    isEmpty = playlistSources.isEmpty()
                )
                Tab.FAVORITES -> ChannelListTab(
                    channels = favoriteChannels,
                    currentChannel = uiState.currentChannel,
                    onChannelClick = { viewModel.playChannel(it) },
                    onFavoriteClick = { viewModel.toggleFavorite(it) },
                    emptyMessage = "No favorites yet"
                )
                Tab.RECENT -> ChannelListTab(
                    channels = recentChannels,
                    currentChannel = uiState.currentChannel,
                    onChannelClick = { viewModel.playChannel(it) },
                    onFavoriteClick = { viewModel.toggleFavorite(it) },
                    emptyMessage = "No recent channels"
                )
                Tab.SETTINGS -> SettingsTab(
                    sources = playlistSources,
                    onDeleteSource = { viewModel.deletePlaylist(it) },
                    onAddPlaylist = { viewModel.showAddPlaylistDialog(true) }
                )
            }

            // Loading Overlay
            if (uiState.isLoading) {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(MaterialTheme.colorScheme.background.copy(alpha = 0.7f)),
                    contentAlignment = Alignment.Center
                ) {
                    CircularProgressIndicator()
                }
            }
        }
    }

    // Add Playlist Dialog
    if (uiState.showAddPlaylistDialog) {
        AddPlaylistDialog(
            onDismiss = { viewModel.showAddPlaylistDialog(false) },
            onAddM3U = { name, url, epg -> viewModel.addM3UPlaylist(name, url, epg) },
            onAddXtream = { name, server, user, pass ->
                viewModel.addXtreamPlaylist(name, server, user, pass)
            }
        )
    }

    // Error Dialog
    uiState.errorMessage?.let { error ->
        AlertDialog(
            onDismissRequest = { viewModel.clearError() },
            title = { Text("Error") },
            text = { Text(error) },
            confirmButton = {
                TextButton(onClick = { viewModel.clearError() }) {
                    Text("OK")
                }
            }
        )
    }
}

@Composable
fun ChannelsTab(
    channelGroups: List<ChannelGroup>,
    searchQuery: String,
    currentChannel: Channel?,
    onSearchQueryChange: (String) -> Unit,
    onChannelClick: (Channel) -> Unit,
    onFavoriteClick: (Channel) -> Unit,
    onRefresh: () -> Unit,
    isLoading: Boolean,
    isEmpty: Boolean
) {
    Column(modifier = Modifier.fillMaxSize()) {
        // Search Bar
        OutlinedTextField(
            value = searchQuery,
            onValueChange = onSearchQueryChange,
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            placeholder = { Text("Search channels...") },
            leadingIcon = { Icon(Icons.Default.Search, null) },
            trailingIcon = {
                if (searchQuery.isNotEmpty()) {
                    IconButton(onClick = { onSearchQueryChange("") }) {
                        Icon(Icons.Default.Clear, null)
                    }
                }
            },
            singleLine = true,
            shape = RoundedCornerShape(12.dp)
        )

        if (isEmpty) {
            EmptyState(
                icon = Icons.Default.Tv,
                title = "No Playlists",
                message = "Add a playlist to get started"
            )
        } else {
            LazyColumn(
                modifier = Modifier.fillMaxSize(),
                contentPadding = PaddingValues(horizontal = 16.dp, vertical = 8.dp)
            ) {
                channelGroups.forEach { group ->
                    item(key = "header_${group.name}") {
                        Text(
                            text = "${group.name} (${group.channelCount})",
                            style = MaterialTheme.typography.titleSmall,
                            color = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.padding(vertical = 8.dp)
                        )
                    }

                    items(group.channels, key = { it.id }) { channel ->
                        ChannelItem(
                            channel = channel,
                            isPlaying = currentChannel?.id == channel.id,
                            onClick = { onChannelClick(channel) },
                            onFavoriteClick = { onFavoriteClick(channel) }
                        )
                    }
                }
            }
        }
    }
}

@Composable
fun ChannelListTab(
    channels: List<Channel>,
    currentChannel: Channel?,
    onChannelClick: (Channel) -> Unit,
    onFavoriteClick: (Channel) -> Unit,
    emptyMessage: String
) {
    if (channels.isEmpty()) {
        EmptyState(
            icon = Icons.Default.Star,
            title = emptyMessage,
            message = "Channels will appear here"
        )
    } else {
        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(16.dp)
        ) {
            items(channels, key = { it.id }) { channel ->
                ChannelItem(
                    channel = channel,
                    isPlaying = currentChannel?.id == channel.id,
                    onClick = { onChannelClick(channel) },
                    onFavoriteClick = { onFavoriteClick(channel) }
                )
            }
        }
    }
}

@Composable
fun ChannelItem(
    channel: Channel,
    isPlaying: Boolean,
    onClick: () -> Unit,
    onFavoriteClick: () -> Unit
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp)
            .clickable(onClick = onClick),
        colors = CardDefaults.cardColors(
            containerColor = if (isPlaying)
                MaterialTheme.colorScheme.primaryContainer
            else
                MaterialTheme.colorScheme.surfaceVariant
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            // Logo
            AsyncImage(
                model = channel.logoUrl,
                contentDescription = null,
                modifier = Modifier
                    .size(48.dp)
                    .clip(RoundedCornerShape(8.dp))
                    .background(MaterialTheme.colorScheme.surface),
                contentScale = ContentScale.Crop
            )

            // Info
            Column(
                modifier = Modifier
                    .weight(1f)
                    .padding(horizontal = 12.dp)
            ) {
                Text(
                    text = channel.name,
                    style = MaterialTheme.typography.bodyLarge,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Text(
                    text = channel.groupTitle,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    maxLines = 1
                )
            }

            // Playing indicator
            if (isPlaying) {
                Icon(
                    Icons.Default.VolumeUp,
                    null,
                    tint = MaterialTheme.colorScheme.primary,
                    modifier = Modifier.padding(end = 8.dp)
                )
            }

            // Favorite button
            IconButton(onClick = onFavoriteClick) {
                Icon(
                    if (channel.isFavorite) Icons.Default.Star else Icons.Default.StarBorder,
                    null,
                    tint = if (channel.isFavorite)
                        MaterialTheme.colorScheme.tertiary
                    else
                        MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        }
    }
}

@Composable
fun MiniPlayer(
    channel: Channel,
    isPlaying: Boolean,
    onPlayPause: () -> Unit,
    onStop: () -> Unit,
    onClick: () -> Unit
) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = MaterialTheme.colorScheme.surfaceVariant,
        tonalElevation = 4.dp
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .clickable(onClick = onClick)
                .padding(12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            AsyncImage(
                model = channel.logoUrl,
                contentDescription = null,
                modifier = Modifier
                    .size(48.dp)
                    .clip(RoundedCornerShape(8.dp)),
                contentScale = ContentScale.Crop
            )

            Column(
                modifier = Modifier
                    .weight(1f)
                    .padding(horizontal = 12.dp)
            ) {
                Text(
                    text = channel.name,
                    style = MaterialTheme.typography.bodyMedium,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Text(
                    text = if (isPlaying) "Playing" else "Paused",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            IconButton(onClick = onPlayPause) {
                Icon(
                    if (isPlaying) Icons.Default.Pause else Icons.Default.PlayArrow,
                    "Play/Pause"
                )
            }

            IconButton(onClick = onStop) {
                Icon(Icons.Default.Close, "Stop")
            }
        }
    }
}

@Composable
fun SettingsTab(
    sources: List<PlaylistSource>,
    onDeleteSource: (PlaylistSource) -> Unit,
    onAddPlaylist: () -> Unit
) {
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = PaddingValues(16.dp)
    ) {
        item {
            Text(
                text = "Playlists",
                style = MaterialTheme.typography.titleMedium,
                modifier = Modifier.padding(vertical = 8.dp)
            )
        }

        items(sources) { source ->
            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(vertical = 4.dp)
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = source.name,
                            style = MaterialTheme.typography.bodyLarge
                        )
                        Text(
                            text = "${source.channelCount} channels - ${source.type.name}",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }

                    IconButton(onClick = { onDeleteSource(source) }) {
                        Icon(Icons.Default.Delete, "Delete", tint = MaterialTheme.colorScheme.error)
                    }
                }
            }
        }

        item {
            OutlinedButton(
                onClick = onAddPlaylist,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(vertical = 8.dp)
            ) {
                Icon(Icons.Default.Add, null)
                Spacer(Modifier.width(8.dp))
                Text("Add Playlist")
            }
        }

        item {
            Spacer(Modifier.height(16.dp))
            Text(
                text = "About",
                style = MaterialTheme.typography.titleMedium,
                modifier = Modifier.padding(vertical = 8.dp)
            )
        }

        item {
            Card(
                modifier = Modifier.fillMaxWidth()
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Text("Version")
                    Text("1.0.0", color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
    }
}

@Composable
fun EmptyState(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    title: String,
    message: String
) {
    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            Icon(
                icon,
                null,
                modifier = Modifier.size(64.dp),
                tint = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Spacer(Modifier.height(16.dp))
            Text(
                text = title,
                style = MaterialTheme.typography.titleMedium
            )
            Text(
                text = message,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AddPlaylistDialog(
    onDismiss: () -> Unit,
    onAddM3U: (String, String, String?) -> Unit,
    onAddXtream: (String, String, String, String) -> Unit
) {
    var selectedType by remember { mutableStateOf(SourceType.M3U) }
    var name by remember { mutableStateOf("") }
    var url by remember { mutableStateOf("") }
    var username by remember { mutableStateOf("") }
    var password by remember { mutableStateOf("") }
    var epgUrl by remember { mutableStateOf("") }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Add Playlist") },
        text = {
            Column {
                // Type Selector
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceEvenly
                ) {
                    FilterChip(
                        selected = selectedType == SourceType.M3U,
                        onClick = { selectedType = SourceType.M3U },
                        label = { Text("M3U") }
                    )
                    FilterChip(
                        selected = selectedType == SourceType.XTREAM_CODES,
                        onClick = { selectedType = SourceType.XTREAM_CODES },
                        label = { Text("Xtream") }
                    )
                }

                Spacer(Modifier.height(16.dp))

                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text("Playlist Name") },
                    modifier = Modifier.fillMaxWidth()
                )

                Spacer(Modifier.height(8.dp))

                OutlinedTextField(
                    value = url,
                    onValueChange = { url = it },
                    label = {
                        Text(
                            if (selectedType == SourceType.XTREAM_CODES)
                                "Server URL" else "Playlist URL"
                        )
                    },
                    modifier = Modifier.fillMaxWidth()
                )

                if (selectedType == SourceType.XTREAM_CODES) {
                    Spacer(Modifier.height(8.dp))
                    OutlinedTextField(
                        value = username,
                        onValueChange = { username = it },
                        label = { Text("Username") },
                        modifier = Modifier.fillMaxWidth()
                    )

                    Spacer(Modifier.height(8.dp))
                    OutlinedTextField(
                        value = password,
                        onValueChange = { password = it },
                        label = { Text("Password") },
                        modifier = Modifier.fillMaxWidth()
                    )
                }

                Spacer(Modifier.height(8.dp))
                OutlinedTextField(
                    value = epgUrl,
                    onValueChange = { epgUrl = it },
                    label = { Text("EPG URL (Optional)") },
                    modifier = Modifier.fillMaxWidth()
                )
            }
        },
        confirmButton = {
            TextButton(
                onClick = {
                    if (selectedType == SourceType.XTREAM_CODES) {
                        onAddXtream(name, url, username, password)
                    } else {
                        onAddM3U(name, url, epgUrl.ifEmpty { null })
                    }
                },
                enabled = url.isNotEmpty() && (selectedType != SourceType.XTREAM_CODES ||
                        (username.isNotEmpty() && password.isNotEmpty()))
            ) {
                Text("Add")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) {
                Text("Cancel")
            }
        }
    )
}
