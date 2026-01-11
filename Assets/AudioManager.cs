using UnityEngine;
using UnityEngine.Audio;
using System.Collections;



public class AudioManager : MonoBehaviour
{
    [Header("-----------Audio Source----------")]
   [SerializeField] AudioSource musicSource;
   [SerializeField] AudioSource SFXSource;

 [Header("-----------Audio Clip----------")]
   public AudioClip background;
   public AudioClip carRev;
   public AudioClip whoosh;

 
  void Awake()
  {
      foreach (Sounds s in sounds)
      {
        gameObject.AddComponent<AudioSource>();
      }
  }

   public void Start()
   {
       {

        musicSource.loop = true; Debug.Log("Track is looped");
        musicSource.clip = background;
        musicSource.volume = 0f;
        musicSource.Play();
        StartCoroutine(Fade(true, musicSource, 2f, 1f));
        StartCoroutine(Fade(false, musicSource, 2f, 0f));
       }
   }

    public IEnumerator Fade(bool fadeIn, AudioSource musicSource, float duration, float targetVolume)
   {
    if(!fadeIn)
    {
      double lengthOfSource = (double)musicSource.clip.samples/musicSource.clip.frequency;
      yield return new WaitForSecondsRealtime((float)(lengthOfSource-duration));
    }

    float time = 0f;
    float startVol = musicSource.volume;
    while (time<duration)
    {
      time += Time.deltaTime;
      musicSource.volume = Mathf.Lerp(startVol, targetVolume, time/duration);
      yield return null;
       
    }
    yield break;
   }

   public void PlaySFX(AudioClip clip)
   {
    SFXSource.PlayOneShot(clip);
   }

   public void Whoosh(AudioClip whoosh)
    {
        SFXSource.PlayOneShot(whoosh);
    }

   public Sounds[] sounds;
   
   
}
