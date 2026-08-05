#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using Playgama.Common;
using UnityEngine;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Playgama.Modules.Notifications
{
    public class NotificationsModule : MonoBehaviour
    {
#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsNotificationsSupported();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeNotificationsSchedule(string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeNotificationsCancel(string id);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeNotificationsCancelAll();
#endif

        public bool isSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsNotificationsSupported() == "true";
#else
                return false;
#endif
            }
        }

        private Action<bool> _scheduleCallback;
        private Action<bool> _cancelCallback;
        private Action<bool> _cancelAllCallback;

        // Schedules a notification the platform shows after the game is closed.
        // The payload of the notification the game was launched from is available
        // via Bridge.platform.payload.
        public void Schedule(ScheduledNotification notification, Action<bool> onComplete = null)
        {
            _scheduleCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeNotificationsSchedule(BuildOptions(notification));
#else
            OnNotificationsScheduleCompletedFailed();
#endif
        }

        // Cancels a previously scheduled notification with the given id.
        public void Cancel(string id, Action<bool> onComplete = null)
        {
            _cancelCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeNotificationsCancel(id ?? string.Empty);
#else
            OnNotificationsCancelCompletedFailed();
#endif
        }

        // Cancels every notification scheduled by the game.
        public void CancelAll(Action<bool> onComplete = null)
        {
            _cancelAllCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeNotificationsCancelAll();
#else
            OnNotificationsCancelAllCompletedFailed();
#endif
        }

        // Called from JS
        private void OnNotificationsScheduleCompletedSuccess(string result)
        {
            _scheduleCallback?.Invoke(true);
            _scheduleCallback = null;
        }

        private void OnNotificationsScheduleCompletedFailed()
        {
            _scheduleCallback?.Invoke(false);
            _scheduleCallback = null;
        }

        private void OnNotificationsCancelCompletedSuccess(string result)
        {
            _cancelCallback?.Invoke(true);
            _cancelCallback = null;
        }

        private void OnNotificationsCancelCompletedFailed()
        {
            _cancelCallback?.Invoke(false);
            _cancelCallback = null;
        }

        private void OnNotificationsCancelAllCompletedSuccess(string result)
        {
            _cancelAllCallback?.Invoke(true);
            _cancelAllCallback = null;
        }

        private void OnNotificationsCancelAllCompletedFailed()
        {
            _cancelAllCallback?.Invoke(false);
            _cancelAllCallback = null;
        }

        private static string BuildOptions(ScheduledNotification notification)
        {
            var options = new Dictionary<string, object>
            {
                { "id", notification?.id ?? string.Empty },
                { "title", notification?.title ?? string.Empty },
                { "description", notification?.description ?? string.Empty }
            };

            if (notification == null)
            {
                return options.ToJson();
            }

            if (notification.delaySeconds >= 0)
            {
                options.Add("delaySeconds", notification.delaySeconds);
            }

            if (!string.IsNullOrEmpty(notification.image))
            {
                options.Add("image", notification.image);
            }

            if (!string.IsNullOrEmpty(notification.callToAction))
            {
                options.Add("callToAction", notification.callToAction);
            }

            if (!string.IsNullOrEmpty(notification.payload))
            {
                options.Add("payload", notification.payload);
            }

            return options.ToJson();
        }
    }
}
#endif
