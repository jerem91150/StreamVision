package com.streamvision.app.di

import android.content.Context
import androidx.room.Room
import com.streamvision.app.data.local.*
import com.streamvision.app.data.repository.RecommendationEngine
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object AppModule {

    @Provides
    @Singleton
    fun provideAppDatabase(@ApplicationContext context: Context): AppDatabase {
        return Room.databaseBuilder(
            context,
            AppDatabase::class.java,
            "streamvision_db"
        )
            .fallbackToDestructiveMigration()
            .build()
    }

    @Provides
    fun providePlaylistSourceDao(database: AppDatabase): PlaylistSourceDao {
        return database.playlistSourceDao()
    }

    @Provides
    fun provideChannelDao(database: AppDatabase): ChannelDao {
        return database.channelDao()
    }

    @Provides
    fun provideEpgProgramDao(database: AppDatabase): EpgProgramDao {
        return database.epgProgramDao()
    }

    @Provides
    fun provideRecentChannelDao(database: AppDatabase): RecentChannelDao {
        return database.recentChannelDao()
    }

    @Provides
    fun provideWatchHistoryDao(database: AppDatabase): WatchHistoryDao {
        return database.watchHistoryDao()
    }

    @Provides
    fun provideUserPreferencesDao(database: AppDatabase): UserPreferencesDao {
        return database.userPreferencesDao()
    }

    @Provides
    @Singleton
    fun provideRecommendationEngine(database: AppDatabase): RecommendationEngine {
        return RecommendationEngine(database)
    }
}
