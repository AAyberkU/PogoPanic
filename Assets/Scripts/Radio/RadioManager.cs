using System;
using System.Collections.Generic;
using UnityEngine;

namespace RageRunGames.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class RadioManager : MonoBehaviour
    {
        #region Inspector
        [Header("Radio output (single AudioSource)")]
        [SerializeField] private AudioSource radioSource;

        [Serializable]
        public class RadioChannel
        {
            public string          name = "Station";
            public Sprite          icon;          // ← added here
            public List<AudioClip> playlist = new();
        }

        [Header("Stations / Playlists")]
        [SerializeField] private List<RadioChannel> channels = new();

        [Header("Hot‑keys")]
        [SerializeField] private KeyCode nextKey = KeyCode.E;
        [SerializeField] private KeyCode prevKey = KeyCode.Q;
        [SerializeField] private KeyCode muteKey = KeyCode.M;
        #endregion

        int  currentChannel;
        bool isMuted;

        float                RadioClock      => Time.time;
        readonly List<float> channelLengths  = new();

        // Public getters for UI
        public string CurrentChannelName  => channels.Count > 0 ? channels[currentChannel].name  : "";
        public Sprite CurrentChannelIcon  => channels.Count > 0 ? channels[currentChannel].icon  : null;
        public bool   IsMuted             => isMuted;
        public float  ClipProgress01      => radioSource.clip ? radioSource.time / radioSource.clip.length : 0f;

        // ──────────────────────────────────────────────────────────────
        void Awake()
        {
            if (!radioSource) radioSource = GetComponent<AudioSource>();

            foreach (var ch in channels)
                channelLengths.Add(ComputeDuration(ch));
        }

        void Start() => PlayChannel(0);

        void Update()
        {
            HandleInput();

            if (!radioSource.isPlaying && !isMuted)
                PlayNextClipInChannel();
        }

        // ---------------- Input ----------------
        void HandleInput()
        {
            if (Input.GetKeyDown(nextKey)) SwitchChannel(+1);
            if (Input.GetKeyDown(prevKey)) SwitchChannel(-1);
            if (Input.GetKeyDown(muteKey)) ToggleMute();
        }

        // ---------------- Channel logic ----------------
        void SwitchChannel(int dir)
        {
            currentChannel = (currentChannel + dir + channels.Count) % channels.Count;
            PlayChannel(currentChannel);
        }

        void PlayChannel(int idx)
        {
            if (channels.Count == 0) return;

            var  ch      = channels[idx];
            float length = channelLengths[idx];
            if (length <= 0f) return;

            float elapsed   = RadioClock % length;
            int   clipIndex = 0;
            float clipStart = 0f;

            for (int i = 0; i < ch.playlist.Count; i++)
            {
                float cLen = ch.playlist[i].length;
                if (elapsed < clipStart + cLen) { clipIndex = i; break; }
                clipStart += cLen;
            }

            radioSource.clip = ch.playlist[clipIndex];
            radioSource.time = elapsed - clipStart;

            if (!isMuted) radioSource.Play();
        }

        void PlayNextClipInChannel() => PlayChannel(currentChannel);

        // ---------------- Mute ----------------
        void ToggleMute()
        {
            isMuted = !isMuted;
            radioSource.mute = isMuted;

            if (!isMuted && !radioSource.isPlaying)
                PlayChannel(currentChannel);
        }

        // ---------------- Helpers ----------------
        static float ComputeDuration(RadioChannel ch)
        {
            float total = 0f;
            foreach (var clip in ch.playlist) if (clip) total += clip.length;
            return total;
        }
    }
}
