package com.streamvision.app.ui.screens

import androidx.compose.animation.animateContentSize
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import coil.compose.AsyncImage
import coil.request.ImageRequest
import com.streamvision.app.data.models.*

@Composable
fun RecommendationsScreen(
    sections: List<RecommendationSection>,
    userStats: UserStats?,
    isLoading: Boolean,
    onChannelClick: (RecommendationItem) -> Unit,
    modifier: Modifier = Modifier
) {
    if (isLoading) {
        Box(
            modifier = modifier.fillMaxSize(),
            contentAlignment = Alignment.Center
        ) {
            CircularProgressIndicator()
        }
        return
    }

    if (sections.isEmpty()) {
        EmptyRecommendationsView(modifier)
        return
    }

    LazyColumn(
        modifier = modifier.fillMaxSize(),
        contentPadding = PaddingValues(vertical = 16.dp)
    ) {
        // User Stats Header
        userStats?.let { stats ->
            item {
                UserStatsCard(stats = stats)
                Spacer(modifier = Modifier.height(24.dp))
            }
        }

        // Recommendation Sections
        items(sections) { section ->
            RecommendationSectionRow(
                section = section,
                onItemClick = onChannelClick
            )
            Spacer(modifier = Modifier.height(24.dp))
        }
    }
}

@Composable
private fun EmptyRecommendationsView(modifier: Modifier = Modifier) {
    Box(
        modifier = modifier.fillMaxSize(),
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            Icon(
                imageVector = Icons.Default.Recommend,
                contentDescription = null,
                modifier = Modifier.size(80.dp),
                tint = MaterialTheme.colorScheme.primary.copy(alpha = 0.5f)
            )
            Spacer(modifier = Modifier.height(16.dp))
            Text(
                text = "Start Watching to Get Recommendations",
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.7f)
            )
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = "We'll learn your preferences as you watch",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.5f)
            )
        }
    }
}

@Composable
private fun UserStatsCard(stats: UserStats) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.primaryContainer
        )
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            Text(
                text = "Your Viewing Stats",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.Bold
            )
            Spacer(modifier = Modifier.height(12.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceEvenly
            ) {
                StatItem(
                    icon = Icons.Default.Timer,
                    value = formatWatchTime(stats.totalWatchTimeMinutes),
                    label = "Watch Time"
                )
                StatItem(
                    icon = Icons.Default.Tv,
                    value = stats.totalChannelsWatched.toString(),
                    label = "Channels"
                )
                StatItem(
                    icon = Icons.Default.Favorite,
                    value = stats.favoriteCategory,
                    label = "Top Category"
                )
            }
        }
    }
}

@Composable
private fun StatItem(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    value: String,
    label: String
) {
    Column(
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary
        )
        Spacer(modifier = Modifier.height(4.dp))
        Text(
            text = value,
            style = MaterialTheme.typography.titleSmall,
            fontWeight = FontWeight.Bold
        )
        Text(
            text = label,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.7f)
        )
    }
}

@Composable
private fun RecommendationSectionRow(
    section: RecommendationSection,
    onItemClick: (RecommendationItem) -> Unit
) {
    Column {
        // Section Header
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Icon(
                imageVector = getSectionIcon(section.type),
                contentDescription = null,
                tint = getSectionColor(section.type),
                modifier = Modifier.size(24.dp)
            )
            Spacer(modifier = Modifier.width(8.dp))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = section.title,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold
                )
                Text(
                    text = section.subtitle,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.6f)
                )
            }
        }

        Spacer(modifier = Modifier.height(12.dp))

        // Horizontal scroll of items
        LazyRow(
            contentPadding = PaddingValues(horizontal = 16.dp),
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            items(section.items) { item ->
                RecommendationCard(
                    item = item,
                    onClick = { onItemClick(item) }
                )
            }
        }
    }
}

@Composable
private fun RecommendationCard(
    item: RecommendationItem,
    onClick: () -> Unit
) {
    Card(
        modifier = Modifier
            .width(160.dp)
            .clickable(onClick = onClick)
            .animateContentSize(),
        shape = RoundedCornerShape(12.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
    ) {
        Column {
            // Thumbnail / Logo
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(100.dp)
                    .background(
                        Brush.linearGradient(
                            colors = listOf(
                                getCategoryColor(item.category),
                                getCategoryColor(item.category).copy(alpha = 0.7f)
                            )
                        )
                    ),
                contentAlignment = Alignment.Center
            ) {
                if (item.logoUrl != null) {
                    AsyncImage(
                        model = ImageRequest.Builder(LocalContext.current)
                            .data(item.logoUrl)
                            .crossfade(true)
                            .build(),
                        contentDescription = item.channelName,
                        modifier = Modifier
                            .size(60.dp)
                            .clip(CircleShape),
                        contentScale = ContentScale.Crop
                    )
                } else {
                    Icon(
                        imageVector = Icons.Default.Tv,
                        contentDescription = null,
                        modifier = Modifier.size(48.dp),
                        tint = Color.White
                    )
                }

                // Progress indicator for continue watching
                if (item.type == RecommendationType.CONTINUE_WATCHING && item.watchedPercentage > 0) {
                    Box(
                        modifier = Modifier
                            .align(Alignment.BottomCenter)
                            .fillMaxWidth()
                            .height(4.dp)
                            .background(Color.Black.copy(alpha = 0.3f))
                    ) {
                        Box(
                            modifier = Modifier
                                .fillMaxHeight()
                                .fillMaxWidth(item.watchedPercentage / 100f)
                                .background(MaterialTheme.colorScheme.primary)
                        )
                    }
                }

                // Score badge
                if (item.score > 0.8) {
                    Surface(
                        modifier = Modifier
                            .align(Alignment.TopEnd)
                            .padding(8.dp),
                        shape = RoundedCornerShape(4.dp),
                        color = Color(0xFFFFD700)
                    ) {
                        Text(
                            text = "TOP",
                            modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp),
                            style = MaterialTheme.typography.labelSmall,
                            fontWeight = FontWeight.Bold,
                            color = Color.Black
                        )
                    }
                }
            }

            // Info
            Column(
                modifier = Modifier.padding(12.dp)
            ) {
                Text(
                    text = item.channelName,
                    style = MaterialTheme.typography.bodyMedium,
                    fontWeight = FontWeight.SemiBold,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Spacer(modifier = Modifier.height(2.dp))
                Text(
                    text = item.category,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.6f),
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
                Spacer(modifier = Modifier.height(4.dp))
                Text(
                    text = item.reason,
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.primary,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis
                )
            }
        }
    }
}

// Helper functions
private fun getSectionIcon(type: RecommendationType): androidx.compose.ui.graphics.vector.ImageVector {
    return when (type) {
        RecommendationType.CONTINUE_WATCHING -> Icons.Default.PlayCircle
        RecommendationType.BECAUSE_YOU_WATCHED -> Icons.Default.History
        RecommendationType.TOP_PICKS_FOR_YOU -> Icons.Default.Star
        RecommendationType.TRENDING_NOW -> Icons.Default.TrendingUp
        RecommendationType.NEW_RELEASES -> Icons.Default.NewReleases
        RecommendationType.CATEGORY_RECOMMENDATION -> Icons.Default.Category
        RecommendationType.HIDDEN_GEMS -> Icons.Default.Diamond
        RecommendationType.SIMILAR_CONTENT -> Icons.Default.CompareArrows
        RecommendationType.TIME_BASED_PICKS -> Icons.Default.Schedule
    }
}

private fun getSectionColor(type: RecommendationType): Color {
    return when (type) {
        RecommendationType.CONTINUE_WATCHING -> Color(0xFF4CAF50)
        RecommendationType.BECAUSE_YOU_WATCHED -> Color(0xFF2196F3)
        RecommendationType.TOP_PICKS_FOR_YOU -> Color(0xFFFF9800)
        RecommendationType.TRENDING_NOW -> Color(0xFFF44336)
        RecommendationType.NEW_RELEASES -> Color(0xFF9C27B0)
        RecommendationType.CATEGORY_RECOMMENDATION -> Color(0xFF00BCD4)
        RecommendationType.HIDDEN_GEMS -> Color(0xFFE91E63)
        RecommendationType.SIMILAR_CONTENT -> Color(0xFF3F51B5)
        RecommendationType.TIME_BASED_PICKS -> Color(0xFF607D8B)
    }
}

private fun getCategoryColor(category: String): Color {
    val hash = category.hashCode()
    val colors = listOf(
        Color(0xFF1E88E5),
        Color(0xFF43A047),
        Color(0xFFE53935),
        Color(0xFF8E24AA),
        Color(0xFFFB8C00),
        Color(0xFF00ACC1),
        Color(0xFF5E35B1),
        Color(0xFFD81B60)
    )
    return colors[kotlin.math.abs(hash) % colors.size]
}

private fun formatWatchTime(minutes: Int): String {
    return when {
        minutes < 60 -> "${minutes}m"
        minutes < 1440 -> "${minutes / 60}h ${minutes % 60}m"
        else -> "${minutes / 1440}d ${(minutes % 1440) / 60}h"
    }
}
