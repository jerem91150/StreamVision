# Plan de Développement Complet - StreamVision Web

## État Actuel

### Fonctionnel
- **Authentification** : Login, Register, Logout, Refresh tokens
- **Live TV** : Interface complète avec recherche et filtres
- **Films/Séries** : Interface UI prête (mais données vides)
- **Player vidéo** : HLS streaming complet avec contrôles
- **API Playlists** : CRUD basique implémenté
- **API History** : Sauvegarde de progression

### À Développer
- Parser M3U et intégration Xtream Codes
- Interface de gestion des playlists
- VOD avec données réelles
- Page de recherche
- Profil utilisateur et paramètres
- Guide TV (EPG)
- Système de favoris
- Dashboard dynamique

---

## Phase 1 : Infrastructure Critique (Priorité Haute)

### 1.1 Parser M3U
**Fichiers à créer :**
- `src/lib/parsers/m3u-parser.ts`

**Fonctionnalités :**
- Parser les fichiers M3U/M3U8
- Extraire : nom, logo, groupe, URL stream, EPG ID
- Support des tags EXTINF étendus
- Détection automatique du type (live/movie/series)

### 1.2 Service Xtream Codes
**Fichiers à créer :**
- `src/lib/services/xtream-service.ts`

**Fonctionnalités :**
- Connexion API Xtream (player_api.php)
- Récupération des chaînes live
- Récupération VOD films
- Récupération VOD séries avec épisodes
- Récupération EPG

### 1.3 API de Synchronisation des Playlists
**Fichiers à créer :**
- `src/app/api/playlists/[id]/sync/route.ts`

**Fonctionnalités :**
- Endpoint POST pour déclencher la sync
- Parser M3U ou appeler Xtream selon le type
- Sauvegarder les chaînes en base
- Mettre à jour lastSync

---

## Phase 2 : Interface de Gestion des Playlists

### 2.1 Page Paramètres/Playlists
**Fichiers à créer :**
- `src/app/(main)/settings/page.tsx`
- `src/app/(main)/settings/playlists/page.tsx`
- `src/components/settings/PlaylistForm.tsx`
- `src/components/settings/PlaylistCard.tsx`

**Fonctionnalités :**
- Liste des playlists de l'utilisateur
- Formulaire d'ajout (M3U URL ou Xtream)
- Bouton de synchronisation
- Indicateur de statut (dernière sync, nb chaînes)
- Suppression de playlist

### 2.2 Modèles de Base de Données Étendus
**Modifications schema.prisma :**
- Ajouter modèle `VodItem` pour films/séries
- Ajouter modèle `Episode` pour les épisodes
- Ajouter modèle `EpgProgram` pour le guide TV

---

## Phase 3 : VOD Complet (Films & Séries)

### 3.1 Schéma Base de Données VOD
**Modifications :**
```prisma
model VodItem {
  id          String   @id @default(cuid())
  playlistId  String
  name        String
  type        String   // movie | series
  posterUrl   String?
  backdropUrl String?
  plot        String?
  genre       String?
  year        Int?
  rating      Float?
  duration    Int?
  streamUrl   String?
  xtreamId    String?

  playlist Playlist @relation(...)
  episodes Episode[]
}

model Episode {
  id        String  @id @default(cuid())
  vodItemId String
  seasonNum Int
  episodeNum Int
  name      String
  plot      String?
  streamUrl String
  duration  Int?

  vodItem VodItem @relation(...)
}
```

### 3.2 APIs VOD
**Fichiers à modifier/créer :**
- `src/app/api/vod/movies/route.ts` - Implémenter vraie requête
- `src/app/api/vod/series/route.ts` - Implémenter vraie requête
- `src/app/api/vod/series/[id]/route.ts` - Détail série + épisodes
- `src/app/api/vod/[id]/stream/route.ts` - Stream VOD

### 3.3 Pages Détail
**Fichiers à créer :**
- `src/app/(main)/films/[id]/page.tsx`
- `src/app/(main)/series/[id]/page.tsx`
- `src/components/content/MovieDetail.tsx`
- `src/components/content/SeriesDetail.tsx`
- `src/components/content/EpisodeList.tsx`

---

## Phase 4 : Recherche Globale

### 4.1 API de Recherche
**Fichiers à créer :**
- `src/app/api/search/route.ts`

**Fonctionnalités :**
- Recherche cross-content (chaînes, films, séries)
- Filtres par type de contenu
- Pagination des résultats

### 4.2 Page de Recherche
**Fichiers à créer :**
- `src/app/(main)/search/page.tsx`
- `src/components/search/SearchResults.tsx`
- `src/components/search/SearchFilters.tsx`

---

## Phase 5 : Profil & Paramètres Utilisateur

### 5.1 Page Profil
**Fichiers à créer :**
- `src/app/(main)/profile/page.tsx`
- `src/components/profile/ProfileForm.tsx`
- `src/components/profile/AvatarUpload.tsx`
- `src/components/profile/ChangePassword.tsx`

### 5.2 APIs Profil
**Fichiers à créer :**
- `src/app/api/user/profile/route.ts` - GET/PUT profil
- `src/app/api/user/password/route.ts` - Changement mot de passe
- `src/app/api/user/preferences/route.ts` - Préférences

### 5.3 Page Paramètres Complète
**Sections :**
- Gestion des playlists (Phase 2)
- Préférences de lecture (qualité, langue, autoplay)
- Informations d'abonnement
- Gestion des profils (multi-profils)

---

## Phase 6 : Système de Favoris

### 6.1 APIs Favoris
**Fichiers à créer :**
- `src/app/api/favorites/route.ts` - GET liste, POST ajouter
- `src/app/api/favorites/[id]/route.ts` - DELETE supprimer

### 6.2 Intégration UI
**Modifications :**
- Ajouter bouton favori sur `ChannelCard`
- Ajouter bouton favori sur `ContentCard`
- Ajouter bouton favori sur pages détail

### 6.3 Page Favoris
**Fichiers à créer :**
- `src/app/(main)/favorites/page.tsx`

---

## Phase 7 : Guide TV (EPG)

### 7.1 Parser XMLTV
**Fichiers à créer :**
- `src/lib/parsers/xmltv-parser.ts`

### 7.2 Modèle et API EPG
**Modifications schema.prisma :**
```prisma
model EpgProgram {
  id          String   @id @default(cuid())
  channelEpgId String
  title       String
  description String?
  startTime   DateTime
  endTime     DateTime
  category    String?
  iconUrl     String?
}
```

**Fichiers à créer :**
- `src/app/api/epg/route.ts` - GET programmes
- `src/app/api/epg/sync/route.ts` - Synchronisation EPG

### 7.3 Page Guide TV
**Fichiers à créer :**
- `src/app/(main)/epg/page.tsx`
- `src/components/epg/EpgGrid.tsx`
- `src/components/epg/EpgTimeline.tsx`
- `src/components/epg/ProgramCard.tsx`

---

## Phase 8 : Dashboard Dynamique

### 8.1 APIs Dashboard
**Fichiers à créer :**
- `src/app/api/dashboard/continue-watching/route.ts`
- `src/app/api/dashboard/recommendations/route.ts`
- `src/app/api/dashboard/trending/route.ts`

### 8.2 Amélioration Dashboard
**Modifications :**
- Remplacer données demo par vraies données
- "Continuer à regarder" depuis WatchHistory
- Recommandations basées sur l'historique
- Tendances basées sur popularité

---

## Phase 9 : Améliorations UX

### 9.1 Composants UI Additionnels
- Skeleton loaders améliorés
- Toasts/Notifications
- Modales de confirmation
- Breadcrumbs navigation

### 9.2 État Global (Zustand Store)
**Fichiers à créer :**
- `src/store/user-store.ts`
- `src/store/player-store.ts`
- `src/store/playlist-store.ts`

### 9.3 Thème Sombre/Clair
- Toggle dans les paramètres
- Persistence du choix

---

## Ordre d'Implémentation Recommandé

| Ordre | Phase | Description | Criticité |
|-------|-------|-------------|-----------|
| 1 | 1.1 | Parser M3U | CRITIQUE |
| 2 | 1.2 | Service Xtream | CRITIQUE |
| 3 | 1.3 | API Sync Playlists | CRITIQUE |
| 4 | 2.1 | UI Gestion Playlists | CRITIQUE |
| 5 | 3.1-3.2 | Base de données + APIs VOD | HAUTE |
| 6 | 3.3 | Pages détail films/séries | HAUTE |
| 7 | 4 | Recherche globale | HAUTE |
| 8 | 5 | Profil & Paramètres | MOYENNE |
| 9 | 6 | Favoris | MOYENNE |
| 10 | 7 | Guide TV (EPG) | MOYENNE |
| 11 | 8 | Dashboard dynamique | BASSE |
| 12 | 9 | Améliorations UX | BASSE |

---

## Fichiers à Créer (Résumé)

### Lib/Services (6 fichiers)
- `src/lib/parsers/m3u-parser.ts`
- `src/lib/parsers/xmltv-parser.ts`
- `src/lib/services/xtream-service.ts`
- `src/store/user-store.ts`
- `src/store/player-store.ts`
- `src/store/playlist-store.ts`

### API Routes (15+ fichiers)
- `src/app/api/playlists/[id]/sync/route.ts`
- `src/app/api/vod/series/[id]/route.ts`
- `src/app/api/vod/[id]/stream/route.ts`
- `src/app/api/search/route.ts`
- `src/app/api/user/profile/route.ts`
- `src/app/api/user/password/route.ts`
- `src/app/api/user/preferences/route.ts`
- `src/app/api/favorites/route.ts`
- `src/app/api/favorites/[id]/route.ts`
- `src/app/api/epg/route.ts`
- `src/app/api/epg/sync/route.ts`
- `src/app/api/dashboard/continue-watching/route.ts`
- `src/app/api/dashboard/recommendations/route.ts`
- `src/app/api/dashboard/trending/route.ts`

### Pages (8 fichiers)
- `src/app/(main)/settings/page.tsx`
- `src/app/(main)/settings/playlists/page.tsx`
- `src/app/(main)/search/page.tsx`
- `src/app/(main)/profile/page.tsx`
- `src/app/(main)/favorites/page.tsx`
- `src/app/(main)/epg/page.tsx`
- `src/app/(main)/films/[id]/page.tsx`
- `src/app/(main)/series/[id]/page.tsx`

### Composants (15+ fichiers)
- `src/components/settings/PlaylistForm.tsx`
- `src/components/settings/PlaylistCard.tsx`
- `src/components/content/MovieDetail.tsx`
- `src/components/content/SeriesDetail.tsx`
- `src/components/content/EpisodeList.tsx`
- `src/components/search/SearchResults.tsx`
- `src/components/search/SearchFilters.tsx`
- `src/components/profile/ProfileForm.tsx`
- `src/components/profile/AvatarUpload.tsx`
- `src/components/profile/ChangePassword.tsx`
- `src/components/epg/EpgGrid.tsx`
- `src/components/epg/EpgTimeline.tsx`
- `src/components/epg/ProgramCard.tsx`

---

## Estimation Totale

- **~45 fichiers** à créer
- **~5 fichiers** à modifier significativement
- **Schéma Prisma** : 2-3 nouveaux modèles

Ce plan transformera StreamVision Web d'un prototype en une application IPTV complète et fonctionnelle.
