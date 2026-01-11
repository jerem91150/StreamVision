package com.streamvision.app.ui.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.streamvision.app.data.models.*
import com.streamvision.app.data.repository.StreamRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class MainViewModel @Inject constructor(
    private val repository: StreamRepository
) : ViewModel() {

    // UI State
    private val _uiState = MutableStateFlow(MainUiState())
    val uiState: StateFlow<MainUiState> = _uiState.asStateFlow()

    // Playlist Sources
    val playlistSources = repository.getAllSources()
        .stateIn(viewModelScope, SharingStarted.Lazily, emptyList())

    // Current Channels
    private val _currentSourceId = MutableStateFlow<String?>(null)
    val channels = _currentSourceId.flatMapLatest { sourceId ->
        sourceId?.let { repository.getChannelsBySource(it) } ?: flowOf(emptyList())
    }.stateIn(viewModelScope, SharingStarted.Lazily, emptyList())

    // Favorites
    val favoriteChannels = repository.getFavoriteChannels()
        .stateIn(viewModelScope, SharingStarted.Lazily, emptyList())

    // Recent
    val recentChannels = repository.getRecentChannels()
        .stateIn(viewModelScope, SharingStarted.Lazily, emptyList())

    // Search
    private val _searchQuery = MutableStateFlow("")
    val searchQuery: StateFlow<String> = _searchQuery.asStateFlow()

    val filteredChannels = combine(channels, _searchQuery) { channelList, query ->
        if (query.isEmpty()) {
            channelList
        } else {
            channelList.filter {
                it.name.contains(query, ignoreCase = true) ||
                        it.groupTitle.contains(query, ignoreCase = true)
            }
        }
    }.stateIn(viewModelScope, SharingStarted.Lazily, emptyList())

    // Channel Groups
    val channelGroups = filteredChannels.map { channelList ->
        channelList
            .groupBy { it.groupTitle }
            .map { (name, channels) ->
                ChannelGroup(name, channels.sortedBy { it.order })
            }
            .sortedBy { it.name }
    }.stateIn(viewModelScope, SharingStarted.Lazily, emptyList())

    init {
        viewModelScope.launch {
            playlistSources.collect { sources ->
                if (sources.isNotEmpty() && _currentSourceId.value == null) {
                    _currentSourceId.value = sources.first().id
                }
            }
        }
    }

    fun selectSource(source: PlaylistSource) {
        _currentSourceId.value = source.id
        _uiState.update { it.copy(selectedSource = source) }
    }

    fun updateSearchQuery(query: String) {
        _searchQuery.value = query
    }

    fun playChannel(channel: Channel) {
        _uiState.update {
            it.copy(
                currentChannel = channel,
                isPlaying = true,
                statusMessage = "Playing: ${channel.name}"
            )
        }
        viewModelScope.launch {
            repository.addToRecent(channel.id)
        }
    }

    fun togglePlayPause() {
        _uiState.update { it.copy(isPlaying = !it.isPlaying) }
    }

    fun stop() {
        _uiState.update {
            it.copy(
                currentChannel = null,
                isPlaying = false,
                statusMessage = "Ready"
            )
        }
    }

    fun toggleMute() {
        _uiState.update { it.copy(isMuted = !it.isMuted) }
    }

    fun setVolume(volume: Float) {
        _uiState.update { it.copy(volume = volume) }
    }

    fun previousChannel() {
        val currentChannel = _uiState.value.currentChannel ?: return
        val channelList = filteredChannels.value
        val currentIndex = channelList.indexOf(currentChannel)
        if (currentIndex > 0) {
            playChannel(channelList[currentIndex - 1])
        }
    }

    fun nextChannel() {
        val currentChannel = _uiState.value.currentChannel ?: return
        val channelList = filteredChannels.value
        val currentIndex = channelList.indexOf(currentChannel)
        if (currentIndex < channelList.size - 1) {
            playChannel(channelList[currentIndex + 1])
        }
    }

    fun toggleFavorite(channel: Channel) {
        viewModelScope.launch {
            repository.toggleFavorite(channel)
        }
    }

    fun addM3UPlaylist(name: String, url: String, epgUrl: String?) {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, statusMessage = "Adding playlist...") }

            repository.importM3UPlaylist(name, url, epgUrl)
                .onSuccess { source ->
                    _currentSourceId.value = source.id
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            statusMessage = "${source.channelCount} channels added",
                            selectedSource = source,
                            showAddPlaylistDialog = false
                        )
                    }
                }
                .onFailure { error ->
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            statusMessage = "Error: ${error.message}",
                            errorMessage = error.message
                        )
                    }
                }
        }
    }

    fun addXtreamPlaylist(name: String, serverUrl: String, username: String, password: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, statusMessage = "Connecting...") }

            repository.importXtreamPlaylist(name, serverUrl, username, password)
                .onSuccess { source ->
                    _currentSourceId.value = source.id
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            statusMessage = "${source.channelCount} channels added",
                            selectedSource = source,
                            showAddPlaylistDialog = false
                        )
                    }
                }
                .onFailure { error ->
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            statusMessage = "Error: ${error.message}",
                            errorMessage = error.message
                        )
                    }
                }
        }
    }

    fun refreshPlaylist() {
        val source = _uiState.value.selectedSource ?: return

        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, statusMessage = "Refreshing...") }

            repository.refreshPlaylist(source)
                .onSuccess { count ->
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            statusMessage = "$count channels loaded"
                        )
                    }
                }
                .onFailure { error ->
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            statusMessage = "Error: ${error.message}",
                            errorMessage = error.message
                        )
                    }
                }
        }
    }

    fun deletePlaylist(source: PlaylistSource) {
        viewModelScope.launch {
            repository.deleteSource(source)
            if (_currentSourceId.value == source.id) {
                _currentSourceId.value = null
            }
        }
    }

    fun showAddPlaylistDialog(show: Boolean) {
        _uiState.update { it.copy(showAddPlaylistDialog = show) }
    }

    fun showFullScreenPlayer(show: Boolean) {
        _uiState.update { it.copy(showFullScreenPlayer = show) }
    }

    fun clearError() {
        _uiState.update { it.copy(errorMessage = null) }
    }
}

data class MainUiState(
    val selectedSource: PlaylistSource? = null,
    val currentChannel: Channel? = null,
    val currentProgram: EpgProgram? = null,
    val isPlaying: Boolean = false,
    val isMuted: Boolean = false,
    val volume: Float = 1f,
    val isLoading: Boolean = false,
    val statusMessage: String = "Ready",
    val errorMessage: String? = null,
    val showAddPlaylistDialog: Boolean = false,
    val showFullScreenPlayer: Boolean = false,
    val currentTab: Tab = Tab.CHANNELS
)

enum class Tab {
    CHANNELS,
    FAVORITES,
    RECENT,
    SETTINGS
}
