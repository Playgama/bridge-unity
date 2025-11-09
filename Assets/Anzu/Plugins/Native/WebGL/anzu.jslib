mergeInto(LibraryManager.library, {
  
    anzu_update: function() {
        window.anzu.update();
    },
  

    anzu_create: function(strPtr) {
        const appKey = UTF8ToString(strPtr);
        window.anzu = new Anzu(appKey);
    },
  

    anzu_initialize: async function() {
        window.anzu.init();
    },


    anzu_setOnRequestScoreCallback: function(callbackPtr) {
        window.anzu.setOnRequestScoreCallback(function() {
            dynCall('v', callbackPtr, []);
        });
    },
  

    anzu_setOnImpressionCallback: function(callbackPtr) {
        window.anzu.setOnImpressionCallback(function(str) {
            var strPtr = _malloc(str.length + 1);
            stringToUTF8(str, strPtr, str.length + 1);
            dynCall('vi', callbackPtr, [strPtr]);
            _free(strPtr);
        });
    },


    anzu_addChannel: function(channelName, gameObjectId, aspectRatio, shrinkToFit, isDynamic, allowImage, allowVideo, onPlayPtr, onPausePtr, onResumePtr, onEmptyPtr) {
		const channelNameStr = UTF8ToString(channelName);
		const gameObjectIdStr = UTF8ToString(gameObjectId);
		const shrinkToFitBool = Boolean(shrinkToFit);
		const isDynamicBool = Boolean(isDynamic);
		const allowImageBool = Boolean(allowImage);
		const allowVideoBool = Boolean(allowVideo);

		const wrapPlay = (channelName, isVideo, url, hasClickAction) => {
			const channelPtr = _malloc(channelName.length + 1);
			stringToUTF8(channelName, channelPtr, channelName.length + 1);
			const urlPtr = _malloc(url.length + 1);
			stringToUTF8(url, urlPtr, url.length + 1);

			dynCall('viiii', onPlayPtr, [
				channelPtr,
				isVideo ? 1 : 0,
				urlPtr,
				hasClickAction ? 1 : 0
			]);

			_free(channelPtr);
			_free(urlPtr);
			return true;
		};

		const wrapStringCallback = (callbackPtr) => {
			return (str) => {
				const strPtr = _malloc(str.length + 1);
				stringToUTF8(str, strPtr, str.length + 1);
				dynCall('vi', callbackPtr, [strPtr]);
				_free(strPtr);
			};
		};

		window.anzu.addChannel(
			channelNameStr,
			gameObjectIdStr,
			aspectRatio,
			shrinkToFitBool,
			isDynamicBool,
			allowImageBool,
			allowVideoBool,
			wrapPlay,
			wrapStringCallback(onPausePtr),
			wrapStringCallback(onResumePtr),
			wrapStringCallback(onEmptyPtr)
		);
    },
  

    anzu_removeChannel: function(channelName) {
        const cn = UTF8ToString(channelName);
        window.anzu.removeChannel(cn);
    },


    anzu_setChannelVisibility: function(channelName, isVisible) {
        const cn = UTF8ToString(channelName);
        window.anzu.setChannelVisibility(cn, isVisible);
    },


    anzu_setChannelPlaybackProgress: function(channelName, progress) {
        const cn = UTF8ToString(channelName);
        window.anzu.setChannelPlaybackProgress(cn, progress);
    },
  

    anzu_setChannelPlaybackMetadata: function(channelName, mediaWidth, mediaHeight, mediaDuration) {
        const cn = UTF8ToString(channelName);
        window.anzu.setChannelPlaybackMetadata(cn, mediaWidth, mediaHeight, mediaDuration);
    },
  

    anzu_setChannelScore: function(channelName, planeSum, screenSum, angle) {
        const cn = UTF8ToString(channelName);
        window.anzu.setChannelScore(cn, planeSum, screenSum, angle);
    },
  

    anzu_clickChannel: function(channelName) {
        const cn = UTF8ToString(channelName);
        window.anzu.clickChannel(cn);
    },


	anzu_setCustomLoggerCallback: function(callbackPtr) {
		window.anzu.setCustomLoggerCallback(function (level) {
			var args = Array.prototype.slice.call(arguments, 1);
			for (var i = 0; i < args.length; i++) {
				if (typeof args[i] === 'object') {
					try {
						args[i] = JSON.stringify(args[i]);
					} catch (e) {
						args[i] = '[Unserializable Object]';
					}
				}
			}
			var msg = args.join(" ");
			var lengthBytes = lengthBytesUTF8(msg) + 1;
			var msgPtr = _malloc(lengthBytes);
			stringToUTF8(msg, msgPtr, lengthBytes);
			dynCall('vii', callbackPtr, [level, msgPtr]);
			_free(msgPtr);
		});
	},


    anzu_setConsentStatus: function(status, consentString) {
        const cstr = UTF8ToString(consentString);
        window.anzu.setConsentStatus(status, cstr);
    },

    anzu_setCoppaRegulated: function() {
        window.anzu.setCoppaRegulated();
    },


    anzu_registerUriSchemaHook: function(schemaPtr, callbackPtr) {
        const s = UTF8ToString(schemaPtr);

        function wrapStringCallback(cb) {
            return function(str) {
                var strPtr = _malloc(str.length + 1);
                stringToUTF8(str, strPtr, str.length + 1);
                dynCall('vi', cb, [strPtr]);
                _free(strPtr);
            };
        }

        window.anzu.registerUriSchemaHook(s, wrapStringCallback(callbackPtr));
    }
});
