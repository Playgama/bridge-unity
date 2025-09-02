mergeInto(LibraryManager.library, {

    PlaygamaBridgeGetPlatformId: function() {
        var platformId = window.getPlatformId()
        var bufferSize = lengthBytesUTF8(platformId) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(platformId, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeGetPlatformLanguage: function() {
        var platformLanguage = window.getPlatformLanguage()
        var bufferSize = lengthBytesUTF8(platformLanguage) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(platformLanguage, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeGetPlatformPayload: function() {
        var platformPayload = window.getPlatformPayload()
        var bufferSize = lengthBytesUTF8(platformPayload) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(platformPayload, buffer, bufferSize)
        return buffer
    },
    
    PlaygamaBridgeGetPlatformTld: function() {
        var platformTld = window.getPlatformTld()
        var bufferSize = lengthBytesUTF8(platformTld) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(platformTld, buffer, bufferSize)
        return buffer
    },
    
    PlaygamaBridgeIsPlatformAudioEnabled: function() {
        var isAudioEnabled = window.getIsPlatformAudioEnabled()
        var bufferSize = lengthBytesUTF8(isAudioEnabled) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isAudioEnabled, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsPlatformGetAllGamesSupported: function() {
        var isAllGamesSupported = window.getIsPlatformGetAllGamesSupported()
        var bufferSize = lengthBytesUTF8(isAllGamesSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isAllGamesSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsPlatformGetGameByIdSupported: function() {
        var isGameByIdSupported = window.getIsPlatformGetGameByIdSupported()
        var bufferSize = lengthBytesUTF8(isGameByIdSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isGameByIdSupported, buffer, bufferSize)
        return buffer
    },
    
    PlaygamaBridgeSendMessageToPlatform: function(message) {
        window.sendMessageToPlatform(UTF8ToString(message))
    },
    
    PlaygamaBridgeGetServerTime: function() {
        window.getServerTime()
    },

    PlaygamaBridgeGetAllGames: function() {
        window.getAllGames()
    },

    PlaygamaBridgeGetGameById: function(options) {
        window.getGameById(UTF8ToString(options))
    },

    PlaygamaBridgeGetDeviceType: function() {
        var deviceType = window.getDeviceType()
        var bufferSize = lengthBytesUTF8(deviceType) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(deviceType, buffer, bufferSize)
        return buffer
    },


    PlaygamaBridgeIsPlayerAuthorizationSupported: function() {
        var isPlayerAuthorizationSupported = window.getIsPlayerAuthorizationSupported()
        var bufferSize = lengthBytesUTF8(isPlayerAuthorizationSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isPlayerAuthorizationSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsPlayerAuthorized: function() {
        var isPlayerAuthorized = window.getIsPlayerAuthorized()
        var bufferSize = lengthBytesUTF8(isPlayerAuthorized) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isPlayerAuthorized, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgePlayerId: function() {
        var playerId = window.getPlayerId()
        var bufferSize = lengthBytesUTF8(playerId) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(playerId, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgePlayerName: function() {
        var playerName = window.getPlayerName()
        var bufferSize = lengthBytesUTF8(playerName) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(playerName, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgePlayerPhotos: function() {
        var playerPhotos = window.getPlayerPhotos()
        var bufferSize = lengthBytesUTF8(playerPhotos) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(playerPhotos, buffer, bufferSize)
        return buffer
    },
    
    PlaygamaBridgePlayerExtra: function() {
        var playerExtra = window.getPlayerExtra()
        var bufferSize = lengthBytesUTF8(playerExtra) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(playerExtra, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeAuthorizePlayer: function(options) {
        window.authorizePlayer(UTF8ToString(options))
    },


    PlaygamaBridgeGetVisibilityState: function() {
        var visibilityState = window.getVisibilityState()
        var bufferSize = lengthBytesUTF8(visibilityState) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(visibilityState, buffer, bufferSize)
        return buffer
    },


    PlaygamaBridgeGetStorageDefaultType: function() {
        var storageType = window.getStorageDefaultType()
        var bufferSize = lengthBytesUTF8(storageType) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(storageType, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsStorageSupported: function(storageType) {
        var isStorageSupported = window.getIsStorageSupported(UTF8ToString(storageType))
        var bufferSize = lengthBytesUTF8(isStorageSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isStorageSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsStorageAvailable: function(storageType) {
        var isStorageAvailable = window.getIsStorageAvailable(UTF8ToString(storageType))
        var bufferSize = lengthBytesUTF8(isStorageAvailable) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isStorageAvailable, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeGetStorageData: function(key, storageType) {
        window.getStorageData(UTF8ToString(key), UTF8ToString(storageType))
    },

    PlaygamaBridgeSetStorageData: function(key, value, storageType) {
        window.setStorageData(UTF8ToString(key), UTF8ToString(value), UTF8ToString(storageType))
    },

    PlaygamaBridgeDeleteStorageData: function(key, storageType) {
        window.deleteStorageData(UTF8ToString(key), UTF8ToString(storageType))
    },


    PlaygamaBridgeGetInterstitialState: function() {
        var interstitialState = window.getInterstitialState()
        var bufferSize = lengthBytesUTF8(interstitialState) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(interstitialState, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsBannerSupported: function() {
        var isBannerSupported = window.getIsBannerSupported()
        var bufferSize = lengthBytesUTF8(isBannerSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isBannerSupported, buffer, bufferSize)
        return buffer
    },
        
    PlaygamaBridgeIsInterstitialSupported: function() {
        var isInterstitialSupported = window.getIsInterstitialSupported()
        var bufferSize = lengthBytesUTF8(isInterstitialSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isInterstitialSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeMinimumDelayBetweenInterstitial: function() {
        var minimumDelayBetweenInterstitial = window.getMinimumDelayBetweenInterstitial()
        var bufferSize = lengthBytesUTF8(minimumDelayBetweenInterstitial) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(minimumDelayBetweenInterstitial, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsRewardedSupported: function() {
        var isRewardedSupported = window.getIsRewardedSupported()
        var bufferSize = lengthBytesUTF8(isRewardedSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isRewardedSupported, buffer, bufferSize)
        return buffer
    },
    
    PlaygamaBridgeRewardedPlacement: function() {
        var rewardedPlacement = window.getRewardedPlacement()
        var bufferSize = lengthBytesUTF8(rewardedPlacement) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(rewardedPlacement, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeSetMinimumDelayBetweenInterstitial: function(options) {
        window.setMinimumDelayBetweenInterstitial(UTF8ToString(options))
    },
    
    PlaygamaBridgeShowBanner: function(position, placement) {
        window.showBanner(UTF8ToString(position), UTF8ToString(placement))
    },
        
    PlaygamaBridgeHideBanner: function() {
        window.hideBanner()
    },

    PlaygamaBridgeShowInterstitial: function(placement) {
        window.showInterstitial(UTF8ToString(placement))
    },

    PlaygamaBridgeShowRewarded: function(placement) {
        window.showRewarded(UTF8ToString(placement))
    },
    
    PlaygamaBridgeCheckAdBlock: function() {
        window.checkAdBlock()
    },


    PlaygamaBridgeIsShareSupported: function() {
        var isShareSupported = window.getIsShareSupported()
        var bufferSize = lengthBytesUTF8(isShareSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isShareSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsInviteFriendsSupported: function() {
        var isInviteFriendsSupported = window.getIsInviteFriendsSupported()
        var bufferSize = lengthBytesUTF8(isInviteFriendsSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isInviteFriendsSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsJoinCommunitySupported: function() {
        var isJoinCommunitySupported = window.getIsJoinCommunitySupported()
        var bufferSize = lengthBytesUTF8(isJoinCommunitySupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isJoinCommunitySupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsCreatePostSupported: function() {
        var isCreatePostSupported = window.getIsCreatePostSupported()
        var bufferSize = lengthBytesUTF8(isCreatePostSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isCreatePostSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsAddToHomeScreenSupported: function() {
        var isAddToHomeScreenSupported = window.getIsAddToHomeScreenSupported()
        var bufferSize = lengthBytesUTF8(isAddToHomeScreenSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isAddToHomeScreenSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsAddToFavoritesSupported: function() {
        var isAddToFavoritesSupported = window.getIsAddToFavoritesSupported()
        var bufferSize = lengthBytesUTF8(isAddToFavoritesSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isAddToFavoritesSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsRateSupported: function() {
        var isRateSupported = window.getIsRateSupported()
        var bufferSize = lengthBytesUTF8(isRateSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isRateSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsExternalLinksAllowed: function() {
        var isExternalLinksAllowed = window.getIsExternalLinksAllowed()
        var bufferSize = lengthBytesUTF8(isExternalLinksAllowed) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isExternalLinksAllowed, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeShare: function(options) {
        window.share(UTF8ToString(options))
    },

    PlaygamaBridgeInviteFriends: function(options) {
        window.inviteFriends(UTF8ToString(options))
    },

    PlaygamaBridgeJoinCommunity: function(options) {
        window.joinCommunity(UTF8ToString(options))
    },

    PlaygamaBridgeCreatePost: function(options) {
        window.createPost(UTF8ToString(options))
    },

    PlaygamaBridgeAddToHomeScreen: function() {
        window.addToHomeScreen()
    },

    PlaygamaBridgeAddToFavorites: function() {
        window.addToFavorites()
    },

    PlaygamaBridgeRate: function() {
        window.rate()
    },


    PlaygamaBridgeLeaderboardsType: function() {
        var value = window.getLeaderboardsType()
        var bufferSize = lengthBytesUTF8(value) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(value, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeLeaderboardsSetScore: function(id, score) {
        window.leaderboardsSetScore(UTF8ToString(id), UTF8ToString(score))
    },

    PlaygamaBridgeLeaderboardsGetEntries: function(id) {
        window.leaderboardsGetEntries(UTF8ToString(id))
    },
    
    PlaygamaBridgeLeaderboardsShowNativePopup: function(id) {
        window.leaderboardsShowNativePopup(UTF8ToString(id))
    },
    

    PlaygamaBridgeIsPaymentsSupported: function() {
        var isPaymentsSupported = window.getIsPaymentsSupported()
        var bufferSize = lengthBytesUTF8(isPaymentsSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isPaymentsSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgePaymentsPurchase: function(id) {
        window.paymentsPurchase(UTF8ToString(id))
    },

    PlaygamaBridgePaymentsConsumePurchase: function(id) {
        window.paymentsConsumePurchase(UTF8ToString(id))
    },
    
    PlaygamaBridgePaymentsGetPurchases: function() {
        window.paymentsGetPurchases()
    },
        
    PlaygamaBridgePaymentsGetCatalog: function() {
        window.paymentsGetCatalog()
    },
    
    PlaygamaBridgeIsRemoteConfigSupported: function() {
        var isRemoteConfigSupported = window.getIsRemoteConfigSupported()
        var bufferSize = lengthBytesUTF8(isRemoteConfigSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isRemoteConfigSupported, buffer, bufferSize)
        return buffer
    },
    
    PlaygamaBridgeRemoteConfigGet: function(options) {
        window.remoteConfigGet(UTF8ToString(options))
    },

    PlaygamaBridgeIsAchievementsSupported: function() {
        var isAchievementsSupported = window.getIsAchievementsSupported()
        var bufferSize = lengthBytesUTF8(isAchievementsSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isAchievementsSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsGetAchievementsListSupported: function() {
        var isGetAchievementsListSupported = window.getIsGetAchievementsListSupported()
        var bufferSize = lengthBytesUTF8(isGetAchievementsListSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isGetAchievementsListSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsAchievementsNativePopupSupported: function() {
        var isAchievementsNativePopupSupported = window.getIsAchievementsNativePopupSupported()
        var bufferSize = lengthBytesUTF8(isAchievementsNativePopupSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isAchievementsNativePopupSupported, buffer, bufferSize)
        return buffer
    },
    
    PlaygamaBridgeAchievementsUnlock: function(options) {
        window.achievementsUnlock(UTF8ToString(options))
    },

    PlaygamaBridgeAchievementsShowNativePopup: function(options) {
        window.achievementsShowNativePopup(UTF8ToString(options))
    },
        
    PlaygamaBridgeAchievementsGetList: function(options) {
        window.achievementsGetList(UTF8ToString(options))
    },

});