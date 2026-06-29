#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using Playgama.Common;
using UnityEngine;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Playgama.Modules.Tasks
{
    public class TasksModule : MonoBehaviour
    {
#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeTasksGetTasks();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeTasksAddProgress(string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeTasksClaimReward(string options);
#endif

        private Action<bool, List<Task>> _getTasksCallback;
        private Action<bool, List<Task>> _addProgressCallback;
        private Action<bool, List<TaskReward>> _claimRewardCallback;

        // Returns the full list of active tasks with their current progress.
        public void GetTasks(Action<bool, List<Task>> onComplete = null)
        {
            _getTasksCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeTasksGetTasks();
#else
            OnTasksGetTasksCompletedFailed();
#endif
        }

        // Adds `amount` progress to every active target watching `metric`. The
        // callback receives the tasks that became fully complete on this call.
        public void AddProgress(string metric, int amount = 1, Action<bool, List<Task>> onComplete = null)
        {
            _addProgressCallback = onComplete;

#if !UNITY_EDITOR
            var options = new Dictionary<string, object>
            {
                { "metric", metric },
                { "amount", amount }
            };
            PlaygamaBridgeTasksAddProgress(options.ToJson());
#else
            OnTasksAddProgressCompletedFailed();
#endif
        }

        // Claims a completed task's rewards. The callback receives (true, rewards)
        // when claimed, or (false, null) when the task is not claimable.
        public void ClaimReward(string taskId, Action<bool, List<TaskReward>> onComplete = null)
        {
            _claimRewardCallback = onComplete;

#if !UNITY_EDITOR
            var options = new Dictionary<string, object>
            {
                { "id", taskId }
            };
            PlaygamaBridgeTasksClaimReward(options.ToJson());
#else
            OnTasksClaimRewardCompletedFailed();
#endif
        }

        // Called from JS
        private void OnTasksGetTasksCompletedSuccess(string result)
        {
            _getTasksCallback?.Invoke(true, ParseTasks(result));
            _getTasksCallback = null;
        }

        private void OnTasksGetTasksCompletedFailed()
        {
            _getTasksCallback?.Invoke(false, null);
            _getTasksCallback = null;
        }

        private void OnTasksAddProgressCompletedSuccess(string result)
        {
            _addProgressCallback?.Invoke(true, ParseTasks(result));
            _addProgressCallback = null;
        }

        private void OnTasksAddProgressCompletedFailed()
        {
            _addProgressCallback?.Invoke(false, null);
            _addProgressCallback = null;
        }

        // An empty result means the task was not claimable (not active / not
        // complete / already claimed).
        private void OnTasksClaimRewardCompletedSuccess(string result)
        {
            if (string.IsNullOrEmpty(result))
            {
                _claimRewardCallback?.Invoke(false, null);
            }
            else
            {
                _claimRewardCallback?.Invoke(true, ParseRewards(result));
            }

            _claimRewardCallback = null;
        }

        private void OnTasksClaimRewardCompletedFailed()
        {
            _claimRewardCallback?.Invoke(false, null);
            _claimRewardCallback = null;
        }

        private static List<Task> ParseTasks(string json)
        {
            var result = new List<Task>();

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    // JsonUtility cannot parse a top-level array, so wrap it.
                    var wrapper = JsonUtility.FromJson<TaskListWrapper>("{\"items\":" + json + "}");
                    if (wrapper?.items != null)
                    {
                        result = wrapper.items;
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
            }

            return result;
        }

        private static List<TaskReward> ParseRewards(string json)
        {
            var result = new List<TaskReward>();

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var wrapper = JsonUtility.FromJson<TaskRewardListWrapper>("{\"items\":" + json + "}");
                    if (wrapper?.items != null)
                    {
                        result = wrapper.items;
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
            }

            return result;
        }

        [Serializable]
        private class TaskListWrapper
        {
            public List<Task> items;
        }

        [Serializable]
        private class TaskRewardListWrapper
        {
            public List<TaskReward> items;
        }
    }
}
#endif
