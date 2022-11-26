using System;
using System.Collections;
using System.Text;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using System.Threading;
using System.Threading.Tasks;

//TODO: namespace

public class PlayerEntry // : System.ComponentModel.INotifyPropertyChanged
    {
        public PlayerEntry(string entityId, string displayName)
        {
            this.EntityId = entityId;
            this.DisplayName = displayName;
            this.ChatIndicator = "";
            this.LastMessage = "";
            this.IsMuted = false;
        }

        public string EntityId { get; }
        public string DisplayName { get; }
        public string ChatIndicator
        {
            get
            {
                return this._chatIndicator;
            }
            set
            {
                this._chatIndicator = value;
                // PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ChatIndicator"));
            }
        }

        public string LastMessage
        {
            get
            {
                return this._lastMessage;
            }
            set
            {
                this._lastMessage = value;
                // PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LastMessage"));
            }
        }

        public bool IsMuted
        {
            get
            {
                return this._isMuted;
            }
            set
            {
                this._isMuted = value;
                // PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("MuteButtonText"));
            }
        }

        public string MuteButtonText
        {
            get
            {
                // This button functions as a toggle.
                // If we're muted, the button is an unmute button.
                // If we're not muted, the button is a mute button.
                return this._isMuted ? "🔊" : "🔇";
            }
        }

        // public event PropertyChangedEventHandler PropertyChanged;
        private string _chatIndicator;
        private string _lastMessage;
        private bool _isMuted;
    }

public class PlayFabManager : MonoBehaviour
{
    [Header("Set in Hierarchy")]
    public InputField NetworkDescriptorText;
    public InputField PlayerIndexText;
    private bool cancelPolling = false;
    private const string libraryName = "UnityGGPO";
    // TODO: Store elsewhere
    private const string titleId = "DC33A";
    // Log entries passed from the C++ app layer back to the C# GUI for presentation
        // private ObservableCollection<LogEntry> logEntries;

        // A long running task to poll for new logs from the C++ sample app.
        private CancellationToken logPollingTaskCancellationToken;
        private Task logPollingTask; //TODO: Switch to ienumeration

    // Player data passed from the C++ app layer back to the C# GUI for presentation
    private ObservableCollection<PlayerEntry> playerEntries;
    // A callback used by the C++ sample app to post new messages to log in the GUI.
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void LogCallback(
            [MarshalAs(UnmanagedType.LPStr)] string system,
            [MarshalAs(UnmanagedType.LPStr)] string message
            );
    private LogCallback logCallbackDelegate;

     // Callbacks used by the C++ sample to update the UI with player changes
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void OnPlayerJoinedCallback(
            [MarshalAs(UnmanagedType.LPStr)] string entityId,
            [MarshalAs(UnmanagedType.LPStr)] string displayName
            );
        private OnPlayerJoinedCallback onPlayerJoinedDelegate;

        private delegate void OnPlayerChatIndicatorUpdatedCallback(
            [MarshalAs(UnmanagedType.LPStr)] string entityId,
            bool isLocalChatIndicator,
            Int32 chatIndicator
            );
        private OnPlayerChatIndicatorUpdatedCallback onPlayerChatIndicatorUpdatedDelegate;

        private delegate void OnPlayerTextMessageReceivedCallback(
            [MarshalAs(UnmanagedType.LPStr)] string senderEntityId,
            [MarshalAs(UnmanagedType.LPStr)] string message
            );
        private OnPlayerTextMessageReceivedCallback onPlayerTextMessageReceivedDelegate;

        private delegate void OnPlayerVoiceTranscriptionReceivedCallback(
            [MarshalAs(UnmanagedType.LPStr)] string speakerEntityId,
            [MarshalAs(UnmanagedType.LPStr)] string transcription
            );
        private OnPlayerVoiceTranscriptionReceivedCallback onPlayerVoiceTranscriptionReceivedDelegate;

        private delegate void OnPlayerLeftCallback(
            [MarshalAs(UnmanagedType.LPStr)] string entityId
            );
        private OnPlayerLeftCallback onPlayerLeftDelegate;
    // Start is called before the first frame update
    public void PlayFabLogin()
    {
        byte[] byteContents = Encoding.Unicode.GetBytes(SystemInfo.deviceUniqueIdentifier);
        byte[] hashText = new System.Security.Cryptography.SHA256CryptoServiceProvider().ComputeHash(byteContents);
        string customId = BitConverter.ToInt32(hashText, 0).ToString() + PlayerIndexText.text;

        this.playerEntries = new ObservableCollection<PlayerEntry>();
        // PlayerList.ItemsSource = this.playerEntries;

        this.onPlayerJoinedDelegate = OnPlayerJoined;
        this.onPlayerChatIndicatorUpdatedDelegate = OnPlayerChatIndicatorUpdated;
        this.onPlayerTextMessageReceivedDelegate = OnPlayerTextMessageReceived;
        this.onPlayerVoiceTranscriptionReceivedDelegate = OnPlayerVoiceTranscriptionReceived;
        this.onPlayerLeftDelegate = OnPlayerLeft;

        PlayFab_Init(
            titleId,
            customId,
            this.onPlayerJoinedDelegate,
            this.onPlayerChatIndicatorUpdatedDelegate,
            this.onPlayerTextMessageReceivedDelegate,
            this.onPlayerVoiceTranscriptionReceivedDelegate,
            this.onPlayerLeftDelegate);

        StartCoroutine(PollForNewLogs());

        this.logCallbackDelegate = LogNewMessage;
    }

    public void PlayFabCreateAndJoinNetwork(){
        string networkId = Guid.NewGuid().ToString().Substring(0,5);
        PlayFab_CreateAndJoinPartyNetwork(networkId);
        NetworkDescriptorText.text = networkId;
    }

    public void PlayFabJoinNetwork(){
        PlayFab_JoinPartyNetwork(NetworkDescriptorText.text);
    }

    public void PlayFabLeaveNetwork(){
        PlayFab_LeavePartyNetwork();
    }

    void Update(){

    }

    private IEnumerator PollForNewLogs()
    {
        while (!this.cancelPolling)
        {
            PlayFab_PollLogQueue(this.logCallbackDelegate);
            yield return new WaitForSeconds(.1f);
        }
    }

    [DllImport(libraryName, CallingConvention = CallingConvention.StdCall)]
        private static extern void PlayFab_PollLogQueue(LogCallback logCallback);

    [DllImport(libraryName, CallingConvention = CallingConvention.StdCall)]
        private static extern void PlayFab_Init(
            string titleId,
            string playFabPlayerCustomId,
            OnPlayerJoinedCallback onPlayerJoinedCallback,
            OnPlayerChatIndicatorUpdatedCallback onPlayerChatIndicatorUpdatedCallback,
            OnPlayerTextMessageReceivedCallback onPlayerTextMessageReceivedCallback,
            OnPlayerVoiceTranscriptionReceivedCallback onPlayerVoiceTranscriptionReceivedCallback,
            OnPlayerLeftCallback onPlayerLeftCallback);
    
    [DllImport(libraryName, CallingConvention = CallingConvention.StdCall)]
        private static extern void PlayFab_CreateAndJoinPartyNetwork(string partyNetworkRoomId);

    [DllImport(libraryName, CallingConvention = CallingConvention.StdCall)]
        private static extern void PlayFab_JoinPartyNetwork(string partyNetworkRoomId);

    [DllImport(libraryName, CallingConvention = CallingConvention.StdCall)]
        private static extern void PlayFab_LeavePartyNetwork();

    [DllImport(libraryName, CallingConvention = CallingConvention.StdCall)]
        private static extern void PlayFab_SendChatText(string chatText);

    private void LogNewMessage(string source, string message)
    {
        // this.Dispatcher.VerifyAccess();
        Debug.Log($"Source: {source}, Message: {message}");
        // this.logEntries.Add(new LogEntry(source, message));
    }
    
    private void OnPlayerJoined(string entityId, string displayName)
    {
        // playerEntries is an observable collection and so edits to it and its entries must be done on the Dispatcher thread
        Action addPlayerAction = () => playerEntries.Add(new PlayerEntry(entityId, displayName));
        // this.Dispatcher.BeginInvoke(addPlayerAction);
    }

    private PlayerEntry FindPlayerEntry(string entityId)
    {
        // playerEntries is an observable collection and so edits to it and its entries must be done on the Dispatcher thread
        // this.Dispatcher.VerifyAccess();
        foreach (var playerEntry in playerEntries)
        {
            if (playerEntry.EntityId == entityId)
            {
                return playerEntry;
            }
        }
        return null;
    }

    private void OnPlayerChatIndicatorUpdated(string entityId, bool isLocalChatIndicator, Int32 chatIndicator)
        {
            string chatIndicatorString;

            if (isLocalChatIndicator)
            {
                switch (chatIndicator)
                {
                    case 0: // Party::PartyLocalChatControlChatIndicator::Silent
                        chatIndicatorString = "🔈";
                        break;
                    case 1: // Party::PartyLocalChatControlChatIndicator::Talking
                        chatIndicatorString = "🔊";
                        break;
                    case 2: // Party::PartyLocalChatControlChatIndicator::AudioInputMuted
                        chatIndicatorString = "🔇";
                        break;
                    case 3: // Party::PartyLocalChatControlChatIndicator::NoAudioInput
                        chatIndicatorString = "❌";
                        break;
                    default:
                        chatIndicatorString = "?";
                        break;
                }
            }
            else
            {
                switch (chatIndicator)
                {
                    case 0: // Party::PartyChatControlChatIndicator::Silent
                        chatIndicatorString = "🔈";
                        break;
                    case 1: // Party::PartyChatControlChatIndicator::Talking
                        chatIndicatorString = "🔊";
                        break;
                    case 2: // Party::PartyChatControlChatIndicator::IncomingVoiceDisabled
                        chatIndicatorString = "🛑";
                        break;
                    case 3: // Party::PartyChatControlChatIndicator::IncomingCommunicationsMuted
                        chatIndicatorString = "🤫";
                        break;
                    case 4: // Party::PartyChatControlChatIndicator::NoRemoteInput
                        chatIndicatorString = "❌";
                        break;
                    case 5: // Party::PartyChatControlChatIndicator::RemoteAudioInputMuted
                        chatIndicatorString = "🔇";
                        break;
                    default:
                        chatIndicatorString = "?";
                        break;
               }
            }

            // playerEntries is an observable collection and so edits to it and its entries must be done on the Dispatcher thread
            Action updateChatIndicatorAction = () => FindPlayerEntry(entityId).ChatIndicator = chatIndicatorString;
            // this.Dispatcher.BeginInvoke(updateChatIndicatorAction);
        }

        private void OnPlayerTextMessageReceived(string senderEntityId, string textMessage)
        {
            // playerEntries is an observable collection and so edits to it and its entries must be done on the Dispatcher thread
            Action updatePlayerLastMessageAction = () => FindPlayerEntry(senderEntityId).LastMessage = "[text]: " + textMessage;
            if(UnityGGPO.GGPO.Session.IsStarted())
                UnityGGPO.GGPO.UggProcessMsg(UnityGGPO.GGPO.Session.GetSession(), textMessage);
            // this.Dispatcher.BeginInvoke(updatePlayerLastMessageAction);
        }

        private void OnPlayerVoiceTranscriptionReceived(string speakerEntityId, string transcription)
        {
            // playerEntries is an observable collection and so edits to it and its entries must be done on the Dispatcher thread
            Action updatePlayerLastMessageAction = () => FindPlayerEntry(speakerEntityId).LastMessage = "[voice]: " + transcription;
            // this.Dispatcher.BeginInvoke(updatePlayerLastMessageAction);
        }

        private void OnPlayerLeft(string entityId)
        {
            // playerEntries is an observable collection and so edits to it and its entries must be done on the Dispatcher thread
            Action removePlayerAction = () => playerEntries.Remove(FindPlayerEntry(entityId));
            // this.Dispatcher.BeginInvoke(removePlayerAction);
        }

        private string TakeChatTextInput()
        {
            // string input = this.ChatTextInput.Text;
            // this.ChatTextInput.Text = "";
            string input  = "";
            return input;
        }
}
