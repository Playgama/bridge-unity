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

    PlaygamaBridgeIsPlatformExternalCallsSupported: function() {
        var isExternalCallsSupported = window.getIsPlatformExternalCallsSupported()
        var bufferSize = lengthBytesUTF8(isExternalCallsSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isExternalCallsSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsPlatformExternalLinksAllowed: function() {
        var isExternalLinksAllowed = window.getIsPlatformExternalLinksAllowed()
        var bufferSize = lengthBytesUTF8(isExternalLinksAllowed) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isExternalLinksAllowed, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeSendMessageToPlatform: function(message, options) {
        window.sendMessageToPlatform(UTF8ToString(message), options ? UTF8ToString(options) : undefined)
    },

    PlaygamaBridgeSendCustomMessageToPlatform: function(id, options) {
        window.sendCustomMessageToPlatform(UTF8ToString(id), options ? UTF8ToString(options) : undefined)
    },

    PlaygamaBridgeGetServerTime: function() {
        window.getServerTime()
    },

    PlaygamaBridgeGetDeviceType: function() {
        var deviceType = window.getDeviceType()
        var bufferSize = lengthBytesUTF8(deviceType) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(deviceType, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeGetSafeArea: function() {
        var safeArea = window.getSafeArea()
        var bufferSize = lengthBytesUTF8(safeArea) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(safeArea, buffer, bufferSize)
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

    PlaygamaBridgeIsPlayerGuest: function() {
        var isPlayerGuest = window.getIsPlayerGuest()
        var bufferSize = lengthBytesUTF8(isPlayerGuest) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isPlayerGuest, buffer, bufferSize)
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


    PlaygamaBridgeGetStorageData: function(key) {
        window.getStorageData(UTF8ToString(key))
    },

    PlaygamaBridgeSetStorageData: function(key, value) {
        window.setStorageData(UTF8ToString(key), UTF8ToString(value))
    },

    PlaygamaBridgeDeleteStorageData: function(key) {
        window.deleteStorageData(UTF8ToString(key))
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
    
    PlaygamaBridgeIsAdvancedBannersSupported: function() {
        var isAdvancedBannersSupported = window.getIsAdvancedBannersSupported()
        var bufferSize = lengthBytesUTF8(isAdvancedBannersSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isAdvancedBannersSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeAdvancedBannersState: function() {
        var advancedBannersState = window.getAdvancedBannersState()
        var bufferSize = lengthBytesUTF8(advancedBannersState) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(advancedBannersState, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeShowAdvancedBanners: function(placement) {
        window.showAdvancedBanners(UTF8ToString(placement))
    },

    PlaygamaBridgeHideAdvancedBanners: function() {
        window.hideAdvancedBanners()
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

    PlaygamaBridgeIsAddToHomeScreenRewardSupported: function() {
        var isAddToHomeScreenRewardSupported = window.getIsAddToHomeScreenRewardSupported()
        var bufferSize = lengthBytesUTF8(isAddToHomeScreenRewardSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isAddToHomeScreenRewardSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsAddToFavoritesSupported: function() {
        var isAddToFavoritesSupported = window.getIsAddToFavoritesSupported()
        var bufferSize = lengthBytesUTF8(isAddToFavoritesSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isAddToFavoritesSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsAddToFavoritesRewardSupported: function() {
        var isAddToFavoritesRewardSupported = window.getIsAddToFavoritesRewardSupported()
        var bufferSize = lengthBytesUTF8(isAddToFavoritesRewardSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isAddToFavoritesRewardSupported, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeIsRateSupported: function() {
        var isRateSupported = window.getIsRateSupported()
        var bufferSize = lengthBytesUTF8(isRateSupported) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isRateSupported, buffer, bufferSize)
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

    PlaygamaBridgeGetAddToHomeScreenReward: function() {
        window.getAddToHomeScreenReward()
    },

    PlaygamaBridgeGetAddToFavoritesReward: function() {
        window.getAddToFavoritesReward()
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

    PlaygamaBridgePaymentsPurchase: function(id, options) {
        window.paymentsPurchase(UTF8ToString(id), UTF8ToString(options))
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
    
    PlaygamaBridgeRemoteConfigSetContext: function(parameters) {
        window.remoteConfigSetContext(UTF8ToString(parameters))
    },

    PlaygamaBridgeRemoteConfigGet: function() {
        window.remoteConfigGet()
    },

    PlaygamaBridgeAchievementsUnlock: function(id) {
        window.achievementsUnlock(UTF8ToString(id))
    },

    PlaygamaBridgeAchievementsGetAchievements: function() {
        window.achievementsGetAchievements()
    },

    // tasks
    PlaygamaBridgeTasksGetTasks: function() {
        window.tasksGetTasks()
    },

    PlaygamaBridgeTasksAddProgress: function(options) {
        window.tasksAddProgress(UTF8ToString(options))
    },

    PlaygamaBridgeTasksClaimReward: function(options) {
        window.tasksClaimReward(UTF8ToString(options))
    },

    // daily rewards
    PlaygamaBridgeDailyRewardsGetRewards: function() {
        window.dailyRewardsGetRewards()
    },

    PlaygamaBridgeDailyRewardsGetCurrentDay: function() {
        window.dailyRewardsGetCurrentDay()
    },

    PlaygamaBridgeDailyRewardsGetCurrentReward: function() {
        window.dailyRewardsGetCurrentReward()
    },

    PlaygamaBridgeDailyRewardsClaimCurrentReward: function() {
        window.dailyRewardsClaimCurrentReward()
    },

    // cross-promo
    PlaygamaBridgeIsCrossPromoVisible: function() {
        var isCrossPromoVisible = window.getIsCrossPromoVisible()
        var bufferSize = lengthBytesUTF8(isCrossPromoVisible) + 1
        var buffer = _malloc(bufferSize)
        stringToUTF8(isCrossPromoVisible, buffer, bufferSize)
        return buffer
    },

    PlaygamaBridgeCrossPromoGetGamesList: function() {
        window.crossPromoGetGamesList()
    },

    PlaygamaBridgeCrossPromoShow: function() {
        window.crossPromoShow()
    },

    PlaygamaBridgeCrossPromoHide: function() {
        window.crossPromoHide()
    },

});