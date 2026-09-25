# Audio Studio sources

The editor audio recovery code was written for this project. No external implementation snippets or FMOD SDK sources were copied.

- [Unity 6000.6 AudioSettings](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/audiosettings) — reading and resetting the current audio configuration, and distinguishing external device changes from a Reset callback.
- [Unity 6000.6 audio settings reference](https://docs.unity.com/en-us/engine/6000.6/manual/audio/reference/class-audio-settings) — audio reinitialization and loss of playback state; recovery is therefore restricted to idle Edit mode.

Unity's installed editor and runtime APIs were used for console callbacks, Play Mode state, SessionState and delayed editor updates. The existing optional FMOD integration bridge remains editor-only and does not provide a runtime FMOD backend. No audio middleware code was imported or changed by this recovery feature.
