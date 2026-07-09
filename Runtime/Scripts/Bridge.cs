#if UNITY_WEBGL
using Playgama.Modules.Advertisement;
using Playgama.Modules.Device;
using Playgama.Modules.Leaderboards;
using Playgama.Modules.Payments;
using Playgama.Modules.Achievements;
using Playgama.Modules.Platform;
using Playgama.Modules.Player;
using Playgama.Modules.RemoteConfig;
using Playgama.Modules.Social;
using Playgama.Modules.Storage;
using Playgama.Modules.Tasks;
using Playgama.Modules.DailyRewards;
using Playgama.Modules.CrossPromo;
using UnityEngine;
#if UNITY_EDITOR
using Playgama.Debug;
#endif

namespace Playgama
{
    public class Bridge :  Playgama.Common.Singleton<Bridge>
    {
        public static AdvertisementModule advertisement => instance._advertisement;
        public static StorageModule storage => instance._storage;
        public static PlatformModule platform => instance._platform; 
        public static SocialModule social => instance._social; 
        public static PlayerModule player => instance._player; 
        public static DeviceModule device => instance._device; 
        public static LeaderboardsModule leaderboards => instance._leaderboards; 
        public static PaymentsModule payments => instance._payments; 
        public static AchievementsModule achievements => instance._achievements;
        public static RemoteConfigModule remoteConfig => instance._remoteConfig;
        public static TasksModule tasks => instance._tasks;
        public static DailyRewardsModule dailyRewards => instance._dailyRewards;
        public static CrossPromoModule crossPromo => instance._crossPromo;

        private AdvertisementModule _advertisement;
        private StorageModule _storage;
        private PlatformModule _platform;
        private SocialModule _social;
        private PlayerModule _player;
        private DeviceModule _device;
        private LeaderboardsModule _leaderboards;
        private PaymentsModule _payments;
        private AchievementsModule _achievements;
        private RemoteConfigModule _remoteConfig;
        private TasksModule _tasks;
        private DailyRewardsModule _dailyRewards;
        private CrossPromoModule _crossPromo;

        protected override void Awake()
        {
            base.Awake();
            instance.name = "PlaygamaBridge";
#if UNITY_EDITOR
            DebugWindow.Initialize();
#endif
            _platform = gameObject.AddComponent<PlatformModule>();
            _player = gameObject.AddComponent<PlayerModule>();
            _storage = gameObject.AddComponent<StorageModule>();
            _advertisement = gameObject.AddComponent<AdvertisementModule>();
            _social = gameObject.AddComponent<SocialModule>();
            _device = new DeviceModule();
            _leaderboards = gameObject.AddComponent<LeaderboardsModule>();
            _payments = gameObject.AddComponent<PaymentsModule>();
            _remoteConfig = gameObject.AddComponent<RemoteConfigModule>();
            _achievements = gameObject.AddComponent<AchievementsModule>();
            _tasks = gameObject.AddComponent<TasksModule>();
            _dailyRewards = gameObject.AddComponent<DailyRewardsModule>();
            _crossPromo = gameObject.AddComponent<CrossPromoModule>();
        }

#if UNITY_EDITOR
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticFields()
        {
            _instance = null;
            _isApplicationQuitting = false;
        }
#endif
    }
}
#endif