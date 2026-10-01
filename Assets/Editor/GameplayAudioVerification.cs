using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CatOnASkateboard.AudioStudio;
using FMODUnity;
using UnityEditor;
using UnityEngine;

/// <summary>Temporary integration diagnostics removed after the audio checks finish.</summary>
public static class GameplayAudioVerification
{
    #region State
    private static double started;
    private static FMOD.Studio.EventInstance voice;
    #endregion
    #region Methods
    /// <summary>Checks the actual catalog adapter against the installed bank cache.</summary>
    public static void Probe()
    {
        // Exercise the same public SDK and editor bridge used by the Catalog Play button.
        Type bridge = Type.GetType("CatOnASkateboard.AudioStudio.Editor.FmodEditorBridge, CatOnASkateboard.AudioStudio.Editor", true);
        Type entryType = Type.GetType("CatOnASkateboard.AudioStudio.Editor.FmodCatalogEntry, CatOnASkateboard.AudioStudio.Editor", true);
        IList catalog = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entryType));
        Debug.Log("AUDIO PROBE Available=" + bridge.GetProperty("Available", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null));
        bridge.GetMethod("ReadCatalog", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { catalog });
        Debug.Log("AUDIO PROBE events=" + catalog.Count);
        object selected = null;
        foreach (object entry in catalog)
            if (((string)entryType.GetField("Path", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(entry)).EndsWith("mus_radio"))
                selected = entry;
        Debug.Log("AUDIO PROBE " + bridge.GetMethod("Play", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null,
            new object[] { selected, new Dictionary<string, float> { { "Channels", 1f } }, 0.1f }));
        voice = (FMOD.Studio.EventInstance)bridge.GetField("preview", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Wait;
    }

    /// <summary>Waits for native audio mixing before reading voice and output status.</summary>
    private static void Wait()
    {
        // No sleep blocks Unity's own editor audio updates.
        if (EditorApplication.timeSinceStartup - started < 3d)
            return;
        EditorApplication.update -= Wait;
        voice.getPlaybackState(out FMOD.Studio.PLAYBACK_STATE state);
        EditorUtils.System.getCoreSystem(out FMOD.System core);
        core.getOutput(out FMOD.OUTPUTTYPE output);
        core.getChannelsPlaying(out int channels, out int real);
        Debug.Log("AUDIO PROBE state=" + state + " output=" + output + " channels=" + channels + "/" + real);
        EditorUtils.PreviewStop(voice);
        EditorApplication.Exit(0);
    }
    #endregion
}
