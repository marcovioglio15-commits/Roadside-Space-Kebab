# Audio Studio editor output recovery

Unity can report `FMOD failed to switch back to normal output ... Error initializing output device (60)` from its built-in audio backend even when no FMOD Studio integration is installed.

After this specific error, Audio Studio queues one retry using the current `AudioSettings` configuration. It waits for idle Edit mode, cancels if Play succeeds, and does not reset gameplay audio, change project settings, mute audio, clear the Console or force Play. Repeated errors from the retry cannot trigger a reset loop. A new device change or successful Play entry rearms the next independent attempt. Batch verification sessions do not run automatic recovery.

**Tools > Audio Studio > Retry Editor Audio Output** allows an explicit retry in idle Edit mode. A successful reset response means Unity accepted the configuration; it does not prove that a physical device is now functioning. Retry Play afterward. If error 60 returns, verify the intended Windows output remains available and restart the Editor after restoring it. The intermittent hardware failure was not reproduced during package verification.

The current project audio configuration was left unchanged: stereo, device-selected sample rate, audio enabled. No runtime script was added for this editor recovery.
