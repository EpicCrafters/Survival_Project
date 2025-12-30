#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

[CustomEditor(typeof(SoundsSO))]
public class SoundsSOEditor : Editor
{
    private void OnEnable()
    {
        SoundsSO soundsSO = (SoundsSO)target;
        ref SoundList[] soundList = ref soundsSO.sounds;

        if (soundList == null)
        {
            soundList = new SoundList[Enum.GetNames(typeof(SoundType)).Length];
            EditorUtility.SetDirty(soundsSO);
            return;
        }

        string[] names = Enum.GetNames(typeof(SoundType));
        bool differentSize = names.Length != soundList.Length;

        Dictionary<string, SoundList> soundsDict = new();

        if (differentSize)
        {
            for (int i = 0; i < soundList.Length; ++i)
            {
                if (!string.IsNullOrEmpty(soundList[i].name))
                    soundsDict.Add(soundList[i].name, soundList[i]);
            }
        }

        Array.Resize(ref soundList, names.Length);
        for (int i = 0; i < soundList.Length; i++)
        {
            string currentName = names[i];
            
            // Update name field
            soundList[i].name = currentName;
            
            // Initialize volume if 0
            if (soundList[i].volume == 0) 
                soundList[i].volume = 1;

            if (differentSize)
            {
                if (soundsDict.ContainsKey(currentName))
                {
                    SoundList current = soundsDict[currentName];
                    UpdateElement(ref soundList[i], current.volume, current.sounds, current.mixer);
                }
                else
                {
                    UpdateElement(ref soundList[i], 1, new AudioClip[0], null);
                }
            }
        }
        
        EditorUtility.SetDirty(soundsSO);
    }

    static void UpdateElement(ref SoundList element, float volume, AudioClip[] sounds, AudioMixerGroup mixer)
    {
        element.volume = volume;
        element.sounds = sounds;
        element.mixer = mixer;
    }
}
#endif