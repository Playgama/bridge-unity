var PlaygamaBridgeLib = {
    $PlaygamaBridgeState: {
        initialized: false,
        bridge: null,
        
        STORAGE_DATA_SEPARATOR: '{bridge_data_separator}',
        STORAGE_KEYS_SEPARATOR: '{bridge_keys_separator}',
        STORAGE_VALUES_SEPARATOR: '{bridge_values_separator}',
        
        sendMessageToUnity: function(name, value) {
            if (window.unityInstance !== null) {
                try {
                    window.unityInstance.SendMessage('PlaygamaBridge', name, value);
                } catch(e) {
                    console.error('Failed to send message to Unity:', e);
                }
            }
        },
        
        initialize: function() {
            if (this.initialized) {
                console.log('Playgama Bridge already initialized');
                return;
            }
            
            this.initialized = true;
            console.log('Initializing Playgama Bridge...');
            
            var self = this;
            
            // Load Playgama Bridge script
            var bridgeScript = document.createElement('script');
            bridgeScript.src = 'https://bridge.playgama.com/v1/stable/playgama-bridge.js';
            
            bridgeScript.onload = function() {
                console.log('Playgama Bridge script loaded');
                
                if (typeof bridge === 'undefined') {
                    console.error('Bridge object not found!');
                    self.sendMessageToUnity('OnBridgeReady', 'false');
                    return;
                }
                
                self.bridge = bridge;
                bridge.engine = 'unity';
                
                bridge.initialize()
                    .then(function() {
                        console.log('Playgama Bridge initialized successfully');
                        
                        // Setup event listeners
                        bridge.advertisement.on('banner_state_changed', function(state) {
                            self.sendMessageToUnity('OnBannerStateChanged', state);
                        });
                        bridge.advertisement.on('interstitial_state_changed', function(state) {
                            self.sendMessageToUnity('OnInterstitialStateChanged', state);
                        });
                        bridge.advertisement.on('rewarded_state_changed', function(state) {
                            self.sendMessageToUnity('OnRewardedStateChanged', state);
                        });
                        bridge.game.on('visibility_state_changed', function(state) {
                            self.sendMessageToUnity('OnVisibilityStateChanged', state);
                        });
                        bridge.platform.on('audio_state_changed', function(isEnabled) {
                            self.sendMessageToUnity('OnAudioStateChanged', isEnabled.toString());
                        });
                        bridge.platform.on('pause_state_changed', function(isPaused) {
                            self.sendMessageToUnity('OnPauseStateChanged', isPaused.toString());
                        });
                        
                        // Create all window functions
                        self.setupWindowFunctions();
                        
                        // Notify Unity
                        self.sendMessageToUnity('OnBridgeReady', 'true');
                    })
                    .catch(function(error) {
                        console.error('Failed to initialize Playgama Bridge:', error);
                        self.sendMessageToUnity('OnBridgeReady', 'false');
                    });
            };
            
            bridgeScript.onerror = function() {
                console.error('Failed to load Playgama Bridge script');
                self.sendMessageToUnity('OnBridgeReady', 'false');
            };
            
            document.head.appendChild(bridgeScript);
        },
        
        setupWindowFunctions: function() {
            var self = this;
            var bridge = this.bridge;
            
            // Platform
            window.getPlatformId = function() {
                return bridge.platform.id || '';
            };
            
            window.getPlatformLanguage = function() {
                return bridge.platform.language || '';
            };
            
            window.getPlatformPayload = function() {
                var payload = bridge.platform.payload;
                return typeof payload === 'string' ? payload : '';
            };
            
            window.getPlatformTld = function() {
                var tld = bridge.platform.tld;
                return typeof tld === 'string' ? tld : '';
            };
            
            window.getIsPlatformAudioEnabled = function() {
                return bridge.platform.isAudioEnabled.toString();
            };
            
            window.getIsPlatformGetAllGamesSupported = function() {
                return bridge.platform.isGetAllGamesSupported.toString();
            };
            
            window.getIsPlatformGetGameByIdSupported = function() {
                return bridge.platform.isGetGameByIdSupported.toString();
            };
            
            window.sendMessageToPlatform = function(message) {
                bridge.platform.sendMessage(message);
            };
            
            window.getServerTime = function() {
                bridge.platform.getServerTime()
                    .then(function(result) {
                        self.sendMessageToUnity('OnGetServerTimeCompleted', result.toString());
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnGetServerTimeCompleted', 'false');
                    });
            };
            
            window.getAllGames = function() {
                bridge.platform.getAllGames()
                    .then(function(result) {
                        self.sendMessageToUnity('OnGetAllGamesCompletedSuccess', JSON.stringify(result));
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnGetAllGamesCompletedFailed');
                    });
            };
            
            window.getGameById = function(options) {
                if (options) {
                    options = JSON.parse(options);
                }
                bridge.platform.getGameById(options)
                    .then(function(result) {
                        self.sendMessageToUnity('OnGetGameByIdCompletedSuccess', JSON.stringify(result));
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnGetGameByIdCompletedFailed');
                    });
            };
            
            // Device
            window.getDeviceType = function() {
                return bridge.device.type || '';
            };
            
            // Player
            window.getIsPlayerAuthorizationSupported = function() {
                return bridge.player.isAuthorizationSupported.toString();
            };
            
            window.getIsPlayerAuthorized = function() {
                return bridge.player.isAuthorized.toString();
            };
            
            window.getPlayerId = function() {
                return bridge.player.id ? bridge.player.id.toString() : '';
            };
            
            window.getPlayerName = function() {
                return bridge.player.name ? bridge.player.name.toString() : '';
            };
            
            window.getPlayerPhotos = function() {
                return bridge.player.photos.length > 0 ? JSON.stringify(bridge.player.photos) : '';
            };
            
            window.getPlayerExtra = function() {
                return bridge.player.extra ? JSON.stringify(bridge.player.extra) : '';
            };
            
            window.authorizePlayer = function(options) {
                if (options) {
                    options = JSON.parse(options);
                }
                bridge.player.authorize(options)
                    .then(function() {
                        self.sendMessageToUnity('OnAuthorizeCompleted', 'true');
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnAuthorizeCompleted', 'false');
                    });
            };
            
            // Game
            window.getVisibilityState = function() {
                return bridge.game.visibilityState || '';
            };
            
            // Storage
            window.getStorageDefaultType = function() {
                return bridge.storage.defaultType || '';
            };
            
            window.getIsStorageSupported = function(storageType) {
                return bridge.storage.isSupported(storageType).toString();
            };
            
            window.getIsStorageAvailable = function(storageType) {
                return bridge.storage.isAvailable(storageType).toString();
            };
            
            window.getStorageData = function(key, storageType) {
                var keys = key.split(self.STORAGE_KEYS_SEPARATOR);
                bridge.storage.get(keys, storageType, false)
                    .then(function(data) {
                        if (keys.length > 1) {
                            var values = [];
                            for (var i = 0; i < keys.length; i++) {
                                var value = data[i];
                                if (value) {
                                    if (typeof value !== 'string') {
                                        value = JSON.stringify(value);
                                    }
                                    values.push(value);
                                } else {
                                    values.push('');
                                }
                            }
                            self.sendMessageToUnity('OnGetStorageDataSuccess', key + self.STORAGE_DATA_SEPARATOR + values.join(self.STORAGE_VALUES_SEPARATOR));
                        } else {
                            var result = data[0] ? (typeof data[0] !== 'string' ? JSON.stringify(data[0]) : data[0]) : '';
                            self.sendMessageToUnity('OnGetStorageDataSuccess', key + self.STORAGE_DATA_SEPARATOR + result);
                        }
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnGetStorageDataFailed', key);
                    });
            };
            
            window.setStorageData = function(key, value, storageType) {
                var keys = key.split(self.STORAGE_KEYS_SEPARATOR);
                var values = value.split(self.STORAGE_VALUES_SEPARATOR);
                bridge.storage.set(keys, values, storageType)
                    .then(function() {
                        self.sendMessageToUnity('OnSetStorageDataSuccess', key);
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnSetStorageDataFailed', key);
                    });
            };
            
            window.deleteStorageData = function(key, storageType) {
                var keys = key.split(self.STORAGE_KEYS_SEPARATOR);
                bridge.storage.delete(keys, storageType)
                    .then(function() {
                        self.sendMessageToUnity('OnDeleteStorageDataSuccess', key);
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnDeleteStorageDataFailed', key);
                    });
            };
            
            // Advertisement
            window.getInterstitialState = function() {
                return bridge.advertisement.interstitialState || '';
            };
            
            window.getIsBannerSupported = function() {
                return bridge.advertisement.isBannerSupported.toString();
            };
            
            window.getIsInterstitialSupported = function() {
                return bridge.advertisement.isInterstitialSupported.toString();
            };
            
            window.getMinimumDelayBetweenInterstitial = function() {
                return bridge.advertisement.minimumDelayBetweenInterstitial.toString();
            };
            
            window.setMinimumDelayBetweenInterstitial = function(options) {
                bridge.advertisement.setMinimumDelayBetweenInterstitial(options);
            };
            
            window.getIsRewardedSupported = function() {
                return bridge.advertisement.isRewardedSupported.toString();
            };
            
            window.getRewardedPlacement = function() {
                return bridge.advertisement.rewardedPlacement || '';
            };
            
            window.showBanner = function(position, placement) {
                bridge.advertisement.showBanner(position, placement);
            };
            
            window.hideBanner = function() {
                bridge.advertisement.hideBanner();
            };
            
            window.showInterstitial = function(placement) {
                bridge.advertisement.showInterstitial(placement);
            };
            
            window.showRewarded = function(placement) {
                bridge.advertisement.showRewarded(placement);
            };
            
            window.checkAdBlock = function() {
                bridge.advertisement.checkAdBlock()
                    .then(function(result) {
                        self.sendMessageToUnity('OnCheckAdBlockCompleted', result.toString());
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnCheckAdBlockCompleted', 'false');
                    });
            };
            
            // Social
            window.getIsShareSupported = function() {
                return bridge.social.isShareSupported.toString();
            };
            
            window.getIsInviteFriendsSupported = function() {
                return bridge.social.isInviteFriendsSupported.toString();
            };
            
            window.getIsJoinCommunitySupported = function() {
                return bridge.social.isJoinCommunitySupported.toString();
            };
            
            window.getIsCreatePostSupported = function() {
                return bridge.social.isCreatePostSupported.toString();
            };
            
            window.getIsAddToHomeScreenSupported = function() {
                return bridge.social.isAddToHomeScreenSupported.toString();
            };
            
            window.getIsAddToFavoritesSupported = function() {
                return bridge.social.isAddToFavoritesSupported.toString();
            };
            
            window.getIsRateSupported = function() {
                return bridge.social.isRateSupported.toString();
            };
            
            window.getIsExternalLinksAllowed = function() {
                return bridge.social.isExternalLinksAllowed.toString();
            };
            
            window.share = function(options) {
                if (options) {
                    options = JSON.parse(options);
                }
                bridge.social.share(options)
                    .then(function() {
                        self.sendMessageToUnity('OnShareCompleted', 'true');
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnShareCompleted', 'false');
                    });
            };
            
            window.inviteFriends = function(options) {
                if (options) {
                    options = JSON.parse(options);
                }
                bridge.social.inviteFriends(options)
                    .then(function() {
                        self.sendMessageToUnity('OnInviteFriendsCompleted', 'true');
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnInviteFriendsCompleted', 'false');
                    });
            };
            
            window.joinCommunity = function(options) {
                if (options) {
                    options = JSON.parse(options);
                }
                bridge.social.joinCommunity(options)
                    .then(function() {
                        self.sendMessageToUnity('OnJoinCommunityCompleted', 'true');
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnJoinCommunityCompleted', 'false');
                    });
            };
            
            window.createPost = function(options) {
                if (options) {
                    options = JSON.parse(options);
                }
                bridge.social.createPost(options)
                    .then(function() {
                        self.sendMessageToUnity('OnCreatePostCompleted', 'true');
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnCreatePostCompleted', 'false');
                    });
            };
            
            window.addToHomeScreen = function() {
                bridge.social.addToHomeScreen()
                    .then(function() {
                        self.sendMessageToUnity('OnAddToHomeScreenCompleted', 'true');
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnAddToHomeScreenCompleted', 'false');
                    });
            };
            
            window.addToFavorites = function() {
                bridge.social.addToFavorites()
                    .then(function() {
                        self.sendMessageToUnity('OnAddToFavoritesCompleted', 'true');
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnAddToFavoritesCompleted', 'false');
                    });
            };
            
            window.rate = function() {
                bridge.social.rate()
                    .then(function() {
                        self.sendMessageToUnity('OnRateCompleted', 'true');
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnRateCompleted', 'false');
                    });
            };
            
            // Leaderboards
            window.getLeaderboardsType = function() {
                return bridge.leaderboards.type || '';
            };
            
            window.leaderboardsSetScore = function(id, score) {
                score = parseInt(score);
                bridge.leaderboards.setScore(id, score)
                    .then(function() {
                        self.sendMessageToUnity('OnLeaderboardsSetScoreCompleted', 'true');
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnLeaderboardsSetScoreCompleted', 'false');
                    });
            };
            
            window.leaderboardsGetEntries = function(id) {
                bridge.leaderboards.getEntries(id)
                    .then(function(data) {
                        var result = data ? JSON.stringify(data) : '';
                        self.sendMessageToUnity('OnLeaderboardsGetEntriesCompletedSuccess', result);
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnLeaderboardsGetEntriesCompletedFailed', 'false');
                    });
            };
            
            window.leaderboardsShowNativePopup = function(id) {
                bridge.leaderboards.showNativePopup(id)
                    .then(function() {
                        self.sendMessageToUnity('OnLeaderboardsShowNativePopupCompleted', 'true');
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnLeaderboardsShowNativePopupCompleted', 'false');
                    });
            };
            
            // Payments
            window.getIsPaymentsSupported = function() {
                return bridge.payments.isSupported.toString();
            };
            
            window.paymentsPurchase = function(id, options) {
                if (options) {
                    options = JSON.parse(options);
                }
                bridge.payments.purchase(id, options)
                    .then(function(data) {
                        var result = data ? (typeof data !== 'string' ? JSON.stringify(data) : data) : '';
                        self.sendMessageToUnity('OnPaymentsPurchaseCompleted', result);
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnPaymentsPurchaseFailed', '');
                    });
            };
            
            window.paymentsConsumePurchase = function(id) {
                bridge.payments.consumePurchase(id)
                    .then(function(data) {
                        var result = data ? (typeof data !== 'string' ? JSON.stringify(data) : data) : '';
                        self.sendMessageToUnity('OnPaymentsConsumePurchaseCompleted', result);
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnPaymentsConsumePurchaseFailed', '');
                    });
            };
            
            window.paymentsGetCatalog = function() {
                bridge.payments.getCatalog()
                    .then(function(data) {
                        var result = data ? (typeof data !== 'string' ? JSON.stringify(data) : data) : '';
                        self.sendMessageToUnity('OnPaymentsGetCatalogCompletedSuccess', result);
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnPaymentsGetCatalogCompletedFailed', '');
                    });
            };
            
            window.paymentsGetPurchases = function() {
                bridge.payments.getPurchases()
                    .then(function(data) {
                        var result = data ? (typeof data !== 'string' ? JSON.stringify(data) : data) : '';
                        self.sendMessageToUnity('OnPaymentsGetPurchasesCompletedSuccess', result);
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnPaymentsGetPurchasesCompletedFailed', '');
                    });
            };
            
            // Remote Config
            window.getIsRemoteConfigSupported = function() {
                return bridge.remoteConfig.isSupported.toString();
            };
            
            window.remoteConfigGet = function(options) {
                if (options) {
                    options = JSON.parse(options);
                }
                bridge.remoteConfig.get(options)
                    .then(function(data) {
                        var result = typeof data !== 'string' ? JSON.stringify(data) : data;
                        self.sendMessageToUnity('OnRemoteConfigGetSuccess', result);
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnRemoteConfigGetFailed', '');
                    });
            };
            
            // Achievements
            window.getIsAchievementsSupported = function() {
                return bridge.achievements.isSupported.toString();
            };
            
            window.getIsGetAchievementsListSupported = function() {
                return bridge.achievements.isGetListSupported.toString();
            };
            
            window.getIsAchievementsNativePopupSupported = function() {
                return bridge.achievements.isNativePopupSupported.toString();
            };
            
            window.achievementsUnlock = function(options) {
                if (options) {
                    options = JSON.parse(options);
                }
                bridge.achievements.unlock(options)
                    .then(function() {
                        self.sendMessageToUnity('OnAchievementsUnlockCompleted', 'true');
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnAchievementsUnlockCompleted', 'false');
                    });
            };
            
            window.achievementsShowNativePopup = function(options) {
                if (options) {
                    options = JSON.parse(options);
                }
                bridge.achievements.showNativePopup(options)
                    .then(function() {
                        self.sendMessageToUnity('OnAchievementsShowNativePopupCompleted', 'true');
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnAchievementsShowNativePopupCompleted', 'false');
                    });
            };
            
            window.achievementsGetList = function(options) {
                if (options) {
                    options = JSON.parse(options);
                }
                bridge.achievements.getList(options)
                    .then(function(data) {
                        var result = data ? (typeof data !== 'string' ? JSON.stringify(data) : data) : '';
                        self.sendMessageToUnity('OnAchievementsGetListCompletedSuccess', result);
                    })
                    .catch(function(error) {
                        self.sendMessageToUnity('OnAchievementsGetListCompletedFailed', 'false');
                    });
            };
            
            console.log('All Playgama Bridge window functions created successfully');
        }
    },
    
    // NEW FUNCTION - Check if bridge is already initialized
    CheckBridgeInitialized: function() {
        // Check if bridge object exists
        if (typeof bridge === 'undefined' || bridge === null) {
            return false;
        }
        
        // Check if bridge has been initialized by verifying required properties exist
        try {
            return bridge.platform && typeof bridge.platform.id !== 'undefined';
        } catch(e) {
            return false;
        }
    },
    
    PlaygamaBridgeInitialize__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeInitialize: function() {
        PlaygamaBridgeState.initialize();
    },
    
    PlaygamaBridgeGetPlatformId__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeGetPlatformId: function() {
        PlaygamaBridgeState.initialize();
        var platformId = window.getPlatformId ? window.getPlatformId() : '';
        var bufferSize = lengthBytesUTF8(platformId) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(platformId, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeGetPlatformLanguage__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeGetPlatformLanguage: function() {
        PlaygamaBridgeState.initialize();
        var platformLanguage = window.getPlatformLanguage ? window.getPlatformLanguage() : '';
        var bufferSize = lengthBytesUTF8(platformLanguage) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(platformLanguage, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeGetPlatformPayload__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeGetPlatformPayload: function() {
        PlaygamaBridgeState.initialize();
        var platformPayload = window.getPlatformPayload ? window.getPlatformPayload() : '';
        var bufferSize = lengthBytesUTF8(platformPayload) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(platformPayload, buffer, bufferSize);
        return buffer;
    },
    
    PlaygamaBridgeGetPlatformTld__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeGetPlatformTld: function() {
        PlaygamaBridgeState.initialize();
        var platformTld = window.getPlatformTld ? window.getPlatformTld() : '';
        var bufferSize = lengthBytesUTF8(platformTld) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(platformTld, buffer, bufferSize);
        return buffer;
    },
    
    PlaygamaBridgeIsPlatformAudioEnabled__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsPlatformAudioEnabled: function() {
        PlaygamaBridgeState.initialize();
        var isAudioEnabled = window.getIsPlatformAudioEnabled ? window.getIsPlatformAudioEnabled() : 'false';
        var bufferSize = lengthBytesUTF8(isAudioEnabled) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isAudioEnabled, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsPlatformGetAllGamesSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsPlatformGetAllGamesSupported: function() {
        PlaygamaBridgeState.initialize();
        var isAllGamesSupported = window.getIsPlatformGetAllGamesSupported ? window.getIsPlatformGetAllGamesSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isAllGamesSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isAllGamesSupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsPlatformGetGameByIdSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsPlatformGetGameByIdSupported: function() {
        PlaygamaBridgeState.initialize();
        var isGameByIdSupported = window.getIsPlatformGetGameByIdSupported ? window.getIsPlatformGetGameByIdSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isGameByIdSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isGameByIdSupported, buffer, bufferSize);
        return buffer;
    },
    
    PlaygamaBridgeSendMessageToPlatform__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeSendMessageToPlatform: function(message) {
        PlaygamaBridgeState.initialize();
        if (window.sendMessageToPlatform) {
            window.sendMessageToPlatform(UTF8ToString(message));
        }
    },
    
    PlaygamaBridgeGetServerTime__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeGetServerTime: function() {
        PlaygamaBridgeState.initialize();
        if (window.getServerTime) {
            window.getServerTime();
        }
    },

    PlaygamaBridgeGetAllGames__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeGetAllGames: function() {
        PlaygamaBridgeState.initialize();
        if (window.getAllGames) {
            window.getAllGames();
        }
    },

    PlaygamaBridgeGetGameById__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeGetGameById: function(options) {
        PlaygamaBridgeState.initialize();
        if (window.getGameById) {
            window.getGameById(UTF8ToString(options));
        }
    },

    PlaygamaBridgeGetDeviceType__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeGetDeviceType: function() {
        PlaygamaBridgeState.initialize();
        var deviceType = window.getDeviceType ? window.getDeviceType() : '';
        var bufferSize = lengthBytesUTF8(deviceType) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(deviceType, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsPlayerAuthorizationSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsPlayerAuthorizationSupported: function() {
        PlaygamaBridgeState.initialize();
        var isPlayerAuthorizationSupported = window.getIsPlayerAuthorizationSupported ? window.getIsPlayerAuthorizationSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isPlayerAuthorizationSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isPlayerAuthorizationSupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsPlayerAuthorized__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsPlayerAuthorized: function() {
        PlaygamaBridgeState.initialize();
        var isPlayerAuthorized = window.getIsPlayerAuthorized ? window.getIsPlayerAuthorized() : 'false';
        var bufferSize = lengthBytesUTF8(isPlayerAuthorized) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isPlayerAuthorized, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgePlayerId__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgePlayerId: function() {
        PlaygamaBridgeState.initialize();
        var playerId = window.getPlayerId ? window.getPlayerId() : '';
        var bufferSize = lengthBytesUTF8(playerId) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(playerId, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgePlayerName__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgePlayerName: function() {
        PlaygamaBridgeState.initialize();
        var playerName = window.getPlayerName ? window.getPlayerName() : '';
        var bufferSize = lengthBytesUTF8(playerName) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(playerName, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgePlayerPhotos__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgePlayerPhotos: function() {
        PlaygamaBridgeState.initialize();
        var playerPhotos = window.getPlayerPhotos ? window.getPlayerPhotos() : '';
        var bufferSize = lengthBytesUTF8(playerPhotos) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(playerPhotos, buffer, bufferSize);
        return buffer;
    },
    
    PlaygamaBridgePlayerExtra__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgePlayerExtra: function() {
        PlaygamaBridgeState.initialize();
        var playerExtra = window.getPlayerExtra ? window.getPlayerExtra() : '';
        var bufferSize = lengthBytesUTF8(playerExtra) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(playerExtra, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeAuthorizePlayer__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeAuthorizePlayer: function(options) {
        PlaygamaBridgeState.initialize();
        if (window.authorizePlayer) {
            window.authorizePlayer(UTF8ToString(options));
        }
    },

    PlaygamaBridgeGetVisibilityState__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeGetVisibilityState: function() {
        PlaygamaBridgeState.initialize();
        var visibilityState = window.getVisibilityState ? window.getVisibilityState() : '';
        var bufferSize = lengthBytesUTF8(visibilityState) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(visibilityState, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeGetStorageDefaultType__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeGetStorageDefaultType: function() {
        PlaygamaBridgeState.initialize();
        var storageType = window.getStorageDefaultType ? window.getStorageDefaultType() : '';
        var bufferSize = lengthBytesUTF8(storageType) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(storageType, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsStorageSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsStorageSupported: function(storageType) {
        PlaygamaBridgeState.initialize();
        var isStorageSupported = window.getIsStorageSupported ? window.getIsStorageSupported(UTF8ToString(storageType)) : 'false';
        var bufferSize = lengthBytesUTF8(isStorageSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isStorageSupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsStorageAvailable__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsStorageAvailable: function(storageType) {
        PlaygamaBridgeState.initialize();
        var isStorageAvailable = window.getIsStorageAvailable ? window.getIsStorageAvailable(UTF8ToString(storageType)) : 'false';
        var bufferSize = lengthBytesUTF8(isStorageAvailable) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isStorageAvailable, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeGetStorageData__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeGetStorageData: function(key, storageType) {
        PlaygamaBridgeState.initialize();
        if (window.getStorageData) {
            window.getStorageData(UTF8ToString(key), UTF8ToString(storageType));
        }
    },

    PlaygamaBridgeSetStorageData__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeSetStorageData: function(key, value, storageType) {
        PlaygamaBridgeState.initialize();
        if (window.setStorageData) {
            window.setStorageData(UTF8ToString(key), UTF8ToString(value), UTF8ToString(storageType));
        }
    },

    PlaygamaBridgeDeleteStorageData__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeDeleteStorageData: function(key, storageType) {
        PlaygamaBridgeState.initialize();
        if (window.deleteStorageData) {
            window.deleteStorageData(UTF8ToString(key), UTF8ToString(storageType));
        }
    },

    PlaygamaBridgeGetInterstitialState__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeGetInterstitialState: function() {
        PlaygamaBridgeState.initialize();
        var interstitialState = window.getInterstitialState ? window.getInterstitialState() : '';
        var bufferSize = lengthBytesUTF8(interstitialState) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(interstitialState, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsBannerSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsBannerSupported: function() {
        PlaygamaBridgeState.initialize();
        var isBannerSupported = window.getIsBannerSupported ? window.getIsBannerSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isBannerSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isBannerSupported, buffer, bufferSize);
        return buffer;
    },
        
    PlaygamaBridgeIsInterstitialSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsInterstitialSupported: function() {
        PlaygamaBridgeState.initialize();
        var isInterstitialSupported = window.getIsInterstitialSupported ? window.getIsInterstitialSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isInterstitialSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isInterstitialSupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeMinimumDelayBetweenInterstitial__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeMinimumDelayBetweenInterstitial: function() {
        PlaygamaBridgeState.initialize();
        var minimumDelayBetweenInterstitial = window.getMinimumDelayBetweenInterstitial ? window.getMinimumDelayBetweenInterstitial() : '0';
        var bufferSize = lengthBytesUTF8(minimumDelayBetweenInterstitial) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(minimumDelayBetweenInterstitial, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsRewardedSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsRewardedSupported: function() {
        PlaygamaBridgeState.initialize();
        var isRewardedSupported = window.getIsRewardedSupported ? window.getIsRewardedSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isRewardedSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isRewardedSupported, buffer, bufferSize);
        return buffer;
    },
    
    PlaygamaBridgeRewardedPlacement__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeRewardedPlacement: function() {
        PlaygamaBridgeState.initialize();
        var rewardedPlacement = window.getRewardedPlacement ? window.getRewardedPlacement() : '';
        var bufferSize = lengthBytesUTF8(rewardedPlacement) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(rewardedPlacement, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeSetMinimumDelayBetweenInterstitial__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeSetMinimumDelayBetweenInterstitial: function(options) {
        PlaygamaBridgeState.initialize();
        if (window.setMinimumDelayBetweenInterstitial) {
            window.setMinimumDelayBetweenInterstitial(UTF8ToString(options));
        }
    },
    
    PlaygamaBridgeShowBanner__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeShowBanner: function(position, placement) {
        PlaygamaBridgeState.initialize();
        if (window.showBanner) {
            window.showBanner(UTF8ToString(position), UTF8ToString(placement));
        }
    },
        
    PlaygamaBridgeHideBanner__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeHideBanner: function() {
        PlaygamaBridgeState.initialize();
        if (window.hideBanner) {
            window.hideBanner();
        }
    },

    PlaygamaBridgeShowInterstitial__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeShowInterstitial: function(placement) {
        PlaygamaBridgeState.initialize();
        if (window.showInterstitial) {
            window.showInterstitial(UTF8ToString(placement));
        }
    },

    PlaygamaBridgeShowRewarded__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeShowRewarded: function(placement) {
        PlaygamaBridgeState.initialize();
        if (window.showRewarded) {
            window.showRewarded(UTF8ToString(placement));
        }
    },
    
    PlaygamaBridgeCheckAdBlock__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeCheckAdBlock: function() {
        PlaygamaBridgeState.initialize();
        if (window.checkAdBlock) {
            window.checkAdBlock();
        }
    },

    PlaygamaBridgeIsShareSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsShareSupported: function() {
        PlaygamaBridgeState.initialize();
        var isShareSupported = window.getIsShareSupported ? window.getIsShareSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isShareSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isShareSupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsInviteFriendsSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsInviteFriendsSupported: function() {
        PlaygamaBridgeState.initialize();
        var isInviteFriendsSupported = window.getIsInviteFriendsSupported ? window.getIsInviteFriendsSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isInviteFriendsSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isInviteFriendsSupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsJoinCommunitySupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsJoinCommunitySupported: function() {
        PlaygamaBridgeState.initialize();
        var isJoinCommunitySupported = window.getIsJoinCommunitySupported ? window.getIsJoinCommunitySupported() : 'false';
        var bufferSize = lengthBytesUTF8(isJoinCommunitySupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isJoinCommunitySupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsCreatePostSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsCreatePostSupported: function() {
        PlaygamaBridgeState.initialize();
        var isCreatePostSupported = window.getIsCreatePostSupported ? window.getIsCreatePostSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isCreatePostSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isCreatePostSupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsAddToHomeScreenSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsAddToHomeScreenSupported: function() {
        PlaygamaBridgeState.initialize();
        var isAddToHomeScreenSupported = window.getIsAddToHomeScreenSupported ? window.getIsAddToHomeScreenSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isAddToHomeScreenSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isAddToHomeScreenSupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsAddToFavoritesSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsAddToFavoritesSupported: function() {
        PlaygamaBridgeState.initialize();
        var isAddToFavoritesSupported = window.getIsAddToFavoritesSupported ? window.getIsAddToFavoritesSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isAddToFavoritesSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isAddToFavoritesSupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsRateSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsRateSupported: function() {
        PlaygamaBridgeState.initialize();
        var isRateSupported = window.getIsRateSupported ? window.getIsRateSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isRateSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isRateSupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsExternalLinksAllowed__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsExternalLinksAllowed: function() {
        PlaygamaBridgeState.initialize();
        var isExternalLinksAllowed = window.getIsExternalLinksAllowed ? window.getIsExternalLinksAllowed() : 'false';
        var bufferSize = lengthBytesUTF8(isExternalLinksAllowed) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isExternalLinksAllowed, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeShare__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeShare: function(options) {
        PlaygamaBridgeState.initialize();
        if (window.share) {
            window.share(UTF8ToString(options));
        }
    },

    PlaygamaBridgeInviteFriends__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeInviteFriends: function(options) {
        PlaygamaBridgeState.initialize();
        if (window.inviteFriends) {
            window.inviteFriends(UTF8ToString(options));
        }
    },

    PlaygamaBridgeJoinCommunity__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeJoinCommunity: function(options) {
        PlaygamaBridgeState.initialize();
        if (window.joinCommunity) {
            window.joinCommunity(UTF8ToString(options));
        }
    },

    PlaygamaBridgeCreatePost__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeCreatePost: function(options) {
        PlaygamaBridgeState.initialize();
        if (window.createPost) {
            window.createPost(UTF8ToString(options));
        }
    },

    PlaygamaBridgeAddToHomeScreen__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeAddToHomeScreen: function() {
        PlaygamaBridgeState.initialize();
        if (window.addToHomeScreen) {
            window.addToHomeScreen();
        }
    },

    PlaygamaBridgeAddToFavorites__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeAddToFavorites: function() {
        PlaygamaBridgeState.initialize();
        if (window.addToFavorites) {
            window.addToFavorites();
        }
    },

    PlaygamaBridgeRate__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeRate: function() {
        PlaygamaBridgeState.initialize();
        if (window.rate) {
            window.rate();
        }
    },

    PlaygamaBridgeLeaderboardsType__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeLeaderboardsType: function() {
        PlaygamaBridgeState.initialize();
        var value = window.getLeaderboardsType ? window.getLeaderboardsType() : '';
        var bufferSize = lengthBytesUTF8(value) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(value, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeLeaderboardsSetScore__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeLeaderboardsSetScore: function(id, score) {
        PlaygamaBridgeState.initialize();
        if (window.leaderboardsSetScore) {
            window.leaderboardsSetScore(UTF8ToString(id), UTF8ToString(score));
        }
    },

    PlaygamaBridgeLeaderboardsGetEntries__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeLeaderboardsGetEntries: function(id) {
        PlaygamaBridgeState.initialize();
        if (window.leaderboardsGetEntries) {
            window.leaderboardsGetEntries(UTF8ToString(id));
        }
    },
    
    PlaygamaBridgeLeaderboardsShowNativePopup__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeLeaderboardsShowNativePopup: function(id) {
        PlaygamaBridgeState.initialize();
        if (window.leaderboardsShowNativePopup) {
            window.leaderboardsShowNativePopup(UTF8ToString(id));
        }
    },

    PlaygamaBridgeIsPaymentsSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsPaymentsSupported: function() {
        PlaygamaBridgeState.initialize();
        var isPaymentsSupported = window.getIsPaymentsSupported ? window.getIsPaymentsSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isPaymentsSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isPaymentsSupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgePaymentsPurchase__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgePaymentsPurchase: function(id, options) {
        PlaygamaBridgeState.initialize();
        if (window.paymentsPurchase) {
            window.paymentsPurchase(UTF8ToString(id), UTF8ToString(options));
        }
    },

    PlaygamaBridgePaymentsConsumePurchase__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgePaymentsConsumePurchase: function(id) {
        PlaygamaBridgeState.initialize();
        if (window.paymentsConsumePurchase) {
            window.paymentsConsumePurchase(UTF8ToString(id));
        }
    },
    
    PlaygamaBridgePaymentsGetPurchases__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgePaymentsGetPurchases: function() {
        PlaygamaBridgeState.initialize();
        if (window.paymentsGetPurchases) {
            window.paymentsGetPurchases();
        }
    },
        
    PlaygamaBridgePaymentsGetCatalog__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgePaymentsGetCatalog: function() {
        PlaygamaBridgeState.initialize();
        if (window.paymentsGetCatalog) {
            window.paymentsGetCatalog();
        }
    },
    
    PlaygamaBridgeIsRemoteConfigSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsRemoteConfigSupported: function() {
        PlaygamaBridgeState.initialize();
        var isRemoteConfigSupported = window.getIsRemoteConfigSupported ? window.getIsRemoteConfigSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isRemoteConfigSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isRemoteConfigSupported, buffer, bufferSize);
        return buffer;
    },
    
    PlaygamaBridgeRemoteConfigGet__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeRemoteConfigGet: function(options) {
        PlaygamaBridgeState.initialize();
        if (window.remoteConfigGet) {
            window.remoteConfigGet(UTF8ToString(options));
        }
    },

    PlaygamaBridgeIsAchievementsSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsAchievementsSupported: function() {
        PlaygamaBridgeState.initialize();
        var isAchievementsSupported = window.getIsAchievementsSupported ? window.getIsAchievementsSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isAchievementsSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isAchievementsSupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsGetAchievementsListSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsGetAchievementsListSupported: function() {
        PlaygamaBridgeState.initialize();
        var isGetAchievementsListSupported = window.getIsGetAchievementsListSupported ? window.getIsGetAchievementsListSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isGetAchievementsListSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isGetAchievementsListSupported, buffer, bufferSize);
        return buffer;
    },

    PlaygamaBridgeIsAchievementsNativePopupSupported__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeIsAchievementsNativePopupSupported: function() {
        PlaygamaBridgeState.initialize();
        var isAchievementsNativePopupSupported = window.getIsAchievementsNativePopupSupported ? window.getIsAchievementsNativePopupSupported() : 'false';
        var bufferSize = lengthBytesUTF8(isAchievementsNativePopupSupported) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(isAchievementsNativePopupSupported, buffer, bufferSize);
        return buffer;
    },
    
    PlaygamaBridgeAchievementsUnlock__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeAchievementsUnlock: function(options) {
        PlaygamaBridgeState.initialize();
        if (window.achievementsUnlock) {
            window.achievementsUnlock(UTF8ToString(options));
        }
    },

    PlaygamaBridgeAchievementsShowNativePopup__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeAchievementsShowNativePopup: function(options) {
        PlaygamaBridgeState.initialize();
        if (window.achievementsShowNativePopup) {
            window.achievementsShowNativePopup(UTF8ToString(options));
        }
    },
        
    PlaygamaBridgeAchievementsGetList__deps: ['$PlaygamaBridgeState'],
    PlaygamaBridgeAchievementsGetList: function(options) {
        PlaygamaBridgeState.initialize();
        if (window.achievementsGetList) {
            window.achievementsGetList(UTF8ToString(options));
        }
    },
};

autoAddDeps(PlaygamaBridgeLib, '$PlaygamaBridgeState');
mergeInto(LibraryManager.library, PlaygamaBridgeLib);