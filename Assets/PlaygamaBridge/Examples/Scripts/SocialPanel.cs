using System.Collections.Generic;
using Playgama;
using Playgama.Modules.Social;
using UnityEngine;
using UnityEngine.UI;

namespace Examples
{
    public class SocialPanel : MonoBehaviour
    {
        [SerializeField] private Text _isShareSupported;
        [SerializeField] private Text _isInviteFriendsSupported;
        [SerializeField] private Text _isJoinCommunitySupported;
        [SerializeField] private Text _isAddToFavoritesSupported;
        [SerializeField] private Text _isAddToHomeScreenSupported;
        [SerializeField] private Text _isCreatePostSupported;
        [SerializeField] private Text _isRateSupported;
        [SerializeField] private Text _isExternalLinksAllowed;
        [SerializeField] private Button _shareButton;
        [SerializeField] private Button _inviteFriendsButton;
        [SerializeField] private Button _joinCommunityButton;
        [SerializeField] private Button _addToFavoritesButton;
        [SerializeField] private Button _addToHomeScreenButton;
        [SerializeField] private Button _createPostButton;
        [SerializeField] private Button _rateButton;
        [SerializeField] private GameObject _overlay;

        private void Start()
        {
            _isShareSupported.text = $"Is Share Supported: { Bridge.social.isShareSupported }";
            _isInviteFriendsSupported.text = $"Is Invite Friends Supported: { Bridge.social.isInviteFriendsSupported }";
            _isJoinCommunitySupported.text = $"Is Join Community Supported: { Bridge.social.isJoinCommunitySupported }";
            _isAddToFavoritesSupported.text = $"Is Add To Favorites Supported: { Bridge.social.isAddToFavoritesSupported }";
            _isAddToHomeScreenSupported.text = $"Is Add To Home Screen Supported: { Bridge.social.isAddToHomeScreenSupported }";
            _isCreatePostSupported.text = $"Is Create Post Supported: { Bridge.social.isCreatePostSupported }";
            _isRateSupported.text = $"Is Rate Supported: { Bridge.social.isRateSupported }";
            _isExternalLinksAllowed.text = $"Is External Links Allowed: { Bridge.social.isExternalLinksAllowed }";

            _shareButton.onClick.AddListener(OnShareButtonClicked);
            _inviteFriendsButton.onClick.AddListener(OnInviteFriendsButtonClicked);
            _joinCommunityButton.onClick.AddListener(OnJoinCommunityButtonClicked);
            _addToFavoritesButton.onClick.AddListener(OnAddToFavoritesButtonClicked);
            _addToHomeScreenButton.onClick.AddListener(OnAddToHomeScreenButtonClicked);
            _createPostButton.onClick.AddListener(OnCreatePostButtonClicked);
            _rateButton.onClick.AddListener(OnRateButtonClicked);
        }

        private void OnShareButtonClicked()
        {
            _overlay.SetActive(true);

            var options = new Dictionary<string, object>();
            
            switch (Bridge.platform.id)
            {
                case "vk":
                    options.Add("link", "YOUR_LINK");
                    break;
            }

            Bridge.social.Share(options, _ => { _overlay.SetActive(false); });
        }

        private void OnInviteFriendsButtonClicked()
        {
            _overlay.SetActive(true);
            
            var options = new Dictionary<string, object>();
            
            switch (Bridge.platform.id)
            {
                case "ok":
                    options.Add("text", "Hello World!");
                    break;
            }

            Bridge.social.InviteFriends(options, _ => { _overlay.SetActive(false); });
        }

        private void OnJoinCommunityButtonClicked()
        {
            _overlay.SetActive(true);

            var options = new Dictionary<string, object>();
            
            switch (Bridge.platform.id)
            {
                case "vk":
                    options.Add("groupId", 199747461);
                    break;
                case "ok":
                    options.Add("groupId", 62984239710374);
                    break;
            }

            Bridge.social.JoinCommunity(options, _ => { _overlay.SetActive(false); });
        }

        private void OnAddToFavoritesButtonClicked()
        {
            _overlay.SetActive(true);
            Bridge.social.AddToFavorites(_ => { _overlay.SetActive(false); });
        }

        private void OnAddToHomeScreenButtonClicked()
        {
            _overlay.SetActive(true);
            Bridge.social.AddToHomeScreen(_ => { _overlay.SetActive(false); });
        }

        private void OnCreatePostButtonClicked()
        {
            _overlay.SetActive(true);
            
            var options = new Dictionary<string, object>();
            switch (Bridge.platform.id)
            {
                case "vk":
                    options.Add("message", "Hello World!");
                    options.Add("attachments", "photo-199747461_457239629");
                    break;
                
                case "ok":
                    var media = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            { "type", "text" },
                            { "text", "Hello World!" },
                        },
                        new Dictionary<string, object>
                        {
                            { "type", "link" },
                            { "url", "https://apiok.ru" },
                        },
                        new Dictionary<string, object>
                        {
                            { "type", "poll" },
                            { "question", "Do you like our API?" },
                            { 
                                "answers", 
                                new object[]
                                {
                                    new Dictionary<string, object>
                                    {
                                        { "text", "Yes" },
                                    },
                                    new Dictionary<string, object>
                                    {
                                        { "text", "No" },
                                    }
                                }
                            },
                            { "options", "SingleChoice,AnonymousVoting" },
                        },
                    };
                    
                    options.Add("media", media);
                    break;
            }

            Bridge.social.CreatePost(options, _ => { _overlay.SetActive(false); });
        }

        private void OnRateButtonClicked()
        {
            _overlay.SetActive(true);
            Bridge.social.Rate(_ => { _overlay.SetActive(false); });
        }
    }
}