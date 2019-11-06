using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using UnityEngine;

public class UggTests : MonoBehaviour {
    public int MAX_PLAYERS = 2;

    public int result = -1;
    public int timeout = 1;
    public int player_type = 3;
    public int player_num = 4;
    public string player_ip_address = "127.0.0.1";
    public ushort player_port = 9000;
    public ulong[] inputs = new ulong[] { 3, 4 };
    public int local_player_handle = 0;
    public ulong input = 0;
    public int time = 0;
    public int phandle = 0;
    public int frame_delay = 10;
    public string logText = "";
    public string host_ip = "127.0.0.1";
    public int num_players = 2;
    public int host_port = 0;
    public int local_port = 0;

    IntPtr ggpo;

    readonly static StringBuilder console = new StringBuilder();
    readonly Dictionary<long, NativeArray<byte>> cache = new Dictionary<long, NativeArray<byte>>();

    bool OnEventCallback(IntPtr info) {
        /*
        connected.player = data[1];
        synchronizing.player = data[1];
        synchronizing.count = data[2];
        synchronizing.total = data[3];
        synchronized.player = data[1];
        disconnected.player = data[1]
        timesync.frames_ahead = data[1];
        connection_interrupted.player = data[1];
        connection_interrupted.disconnect_timeout = data[2];
        connection_resumed.player = data[1];
        */

        int[] data = new int[4];
        Marshal.Copy(info, data, 0, 4);
        switch (data[0]) {
            case GGPO.EVENTCODE_CONNECTED_TO_PEER:
                return OnEventConnectedToPeer(data[1]);

            case GGPO.EVENTCODE_SYNCHRONIZING_WITH_PEER:
                return OnEventSynchronizingWithPeer(data[1], data[2], data[3]);

            case GGPO.EVENTCODE_SYNCHRONIZED_WITH_PEER:
                return OnEventSynchronizedWithPeer(data[1]);

            case GGPO.EVENTCODE_RUNNING:
                return OnEventRunning();

            case GGPO.EVENTCODE_DISCONNECTED_FROM_PEER:
                return OnEventDisconnectedFromPeer(data[1]);

            case GGPO.EVENTCODE_TIMESYNC:
                return OnEventTimesync(data[1]);

            case GGPO.EVENTCODE_CONNECTION_INTERRUPTED:
                return OnEventConnectionInterrupted(data[1], data[2]);

            case GGPO.EVENTCODE_CONNECTION_RESUMED:
                return OnEventConnectionResumed(data[1]);
        }
        return false;
    }

    void Start() {
        Log(string.Format("Plugin Version: {0} build {1}", GGPO.Version, GGPO.BuildNumber));
        GGPO.UggSetLogDelegate(Log);
    }

    bool OnBeginGame(string name) {
        Debug.Log($"OnBeginGame({name})");
        return true;
    }

    bool OnAdvanceFrame(int flags) {
        Debug.Log($"OnAdvanceFrame({flags})");
        return true;
    }

    unsafe bool OnSaveGameState(void** buffer, int* outLen, int* outChecksum, int frame) {
        Debug.Log($"OnSaveGameState({frame})");
        var data = new NativeArray<byte>(12, Allocator.Persistent);
        for (int i = 0; i < data.Length; ++i) {
            data[i] = (byte)i;
        }
        var ptr = Helper.ToPtr(data);
        cache[(long)ptr] = data;

        *buffer = ptr;
        *outLen = data.Length;
        *outChecksum = 99;
        return true;
    }

    unsafe bool OnLogGameState(string text, void* dataPtr, int length) {
        // var list = string.Join(",", Array.ConvertAll(data.ToArray(), x => x.ToString()));
        Debug.Log($"OnLogGameState({text})");
        return true;
    }

    unsafe bool OnLoadGameState(void* dataPtr, int length) {
        // var list = string.Join(",", Array.ConvertAll(data.ToArray(), x => x.ToString()));
        Debug.Log($"OnLoadGameState()");
        return true;
    }

    unsafe void OnFreeBuffer(void* dataPtr) {
        Debug.Log($"OnFreeBuffer({(long)dataPtr})");
        if (cache.TryGetValue((long)dataPtr, out var data)) {
            data.Dispose();
        }
    }

    bool OnEventTimesync(int timesync_frames_ahead) {
        Debug.Log($"OnEventEventcodeTimesync({timesync_frames_ahead})");
        return true;
    }

    bool OnEventDisconnectedFromPeer(int disconnected_player) {
        Debug.Log($"OnEventDisconnectedFromPeer({disconnected_player})");
        return true;
    }

    bool OnEventConnectionResumed(int connection_resumed_player) {
        Debug.Log($"OnEventConnectionResumed({connection_resumed_player})");
        return true;
    }

    bool OnEventConnectionInterrupted(int connection_interrupted_player, int connection_interrupted_disconnect_timeout) {
        Debug.Log($"OnEventConnectionInterrupted({connection_interrupted_player},{connection_interrupted_disconnect_timeout})");
        return true;
    }

    bool OnEventRunning() {
        Debug.Log($"OnEventRunning()");
        return true;
    }

    bool OnEventSynchronizedWithPeer(int synchronizing_player) {
        Debug.Log($"OnEventSynchronizedWithPeer({synchronizing_player})");
        return true;
    }

    bool OnEventSynchronizingWithPeer(int synchronizing_player, int synchronizing_count, int synchronizing_total) {
        Debug.Log($"OnEventSynchronizingWithPeer({synchronizing_player}, {synchronizing_count}, {synchronizing_total})");
        return true;
    }

    bool OnEventConnectedToPeer(int connected_player) {
        Debug.Log($"OnEventConnectedToPeer({connected_player})");
        return true;
    }

    public static void Log(string obj) {
        Debug.Log(obj);
        console.Append(obj + "\n");
    }

    void OnGUI() {
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), console.ToString());
    }

    [Button]
    public void RunTest(int testId) {
        switch (testId) {
            case 0:
                unsafe {
                    ggpo = GGPO.UggStartSession(OnBeginGame,
                        OnAdvanceFrame,
                        OnLoadGameState,
                        OnLogGameState,
                        OnSaveGameState,
                        OnFreeBuffer,
                        OnEventCallback,
                        "Tests", num_players, local_port);

                    Debug.Assert(ggpo != IntPtr.Zero);
                }
                break;

            case 1:
                unsafe {
                    ggpo = GGPO.UggStartSpectating(OnBeginGame,
                        OnAdvanceFrame,
                        OnLoadGameState,
                        OnLogGameState,
                        OnSaveGameState,
                        OnFreeBuffer,
                        OnEventCallback,
                        "Tests", num_players, local_port, host_ip, host_port);

                    Debug.Assert(ggpo != IntPtr.Zero);
                }
                break;

            case 2:
                unsafe {
                    result = GGPO.UggTestStartSession(out ggpo, OnBeginGame,
                        OnAdvanceFrame,
                        OnLoadGameState,
                        OnLogGameState,
                        OnSaveGameState,
                        OnFreeBuffer,
                        OnEventCallback,
                        "Tests", num_players, local_port);

                    Debug.Assert(ggpo != IntPtr.Zero);
                }
                break;

            case 3:
                result = GGPO.UggSynchronizeInput(ggpo, inputs, MAX_PLAYERS, out int disconnect_flags);
                Debug.Log($"DllSynchronizeInput{disconnect_flags} {inputs[0]} {inputs[1]}");
                break;

            case 4:
                result = GGPO.UggAddLocalInput(ggpo, local_player_handle, input);
                break;

            case 5:
                foreach (var c in cache.Values) {
                    c.Dispose();
                }
                cache.Clear();
                result = GGPO.UggCloseSession(ggpo);
                break;

            case 6:
                result = GGPO.UggIdle(ggpo, time);
                break;

            case 7:
                result = GGPO.UggAddPlayer(ggpo, player_type, player_num, player_ip_address, player_port, out phandle);
                break;

            case 8:
                result = GGPO.UggDisconnectPlayer(ggpo, phandle);
                break;

            case 9:
                result = GGPO.UggSetFrameDelay(ggpo, phandle, frame_delay);
                break;

            case 10:
                result = GGPO.UggAdvanceFrame(ggpo);
                break;

            case 11:
                result = GGPO.UggGetNetworkStats(ggpo, phandle, out int send_queue_len, out int recv_queue_len, out int ping, out int kbps_sent, out int local_frames_behind, out int remote_frames_behind);
                Debug.Log($"DllSynchronizeInput{send_queue_len}, {recv_queue_len}, {ping}, {kbps_sent}, " +
                    $"{ local_frames_behind}, {remote_frames_behind}");
                break;

            case 12:
                GGPO.UggLog(ggpo, logText);
                result = GGPO.OK;
                break;

            case 13:
                result = GGPO.UggSetDisconnectNotifyStart(ggpo, timeout);
                break;

            case 14:
                result = GGPO.UggSetDisconnectTimeout(ggpo, timeout);
                break;
        }
        GGPO.ReportFailure(result);
    }
}
