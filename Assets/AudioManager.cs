using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using UnityEngine.SceneManagement;
using System;



public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    public Sounds[] sounds;


    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }


        foreach (Sounds s in sounds)
        {

            if (s.clip == null)
            {
                Debug.LogWarning($"AudioManager: Sound '{s.name}' has no Audioclip assigned!");
                continue;
            }

            s.source = gameObject.AddComponent<AudioSource>();
            s.source.clip = s.clip;
            s.source.pitch = s.pitch;
            s.source.volume = s.volume;
            s.source.loop = s.loop;


        }

    }

    public void Start()
    {


    }


    public void Play(string name)
    {
        if (sounds == null) return;


        Sounds s = Array.Find(sounds, sound => sound.name == name);

        if (s == null)
        {
            Debug.LogWarning($"AudioManager: Sound '{name}' not found!");
            return;
        }


        s.source.Play();


    }

    public void Stop(string name)
    {
        if (sounds == null) return;

        Sounds s = Array.Find(sounds, sound => sound.name == name);

        if (s == null)
        {
            Debug.LogWarning($"AudioManager: Sound '{name}' not found!");
            return;
        }

        s.source.Stop();

    }
}







