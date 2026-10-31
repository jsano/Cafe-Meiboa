using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BgmPlayer : MonoBehaviour
{
    [SerializeField] AudioSource source;
    [SerializeField] List<AudioClip> songs = new();

    readonly List<AudioClip> order = new();
    int index;
    double nextAt;
    bool playing;

    void Awake()
    {
        if (source == null)
            source = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (source == null || !HasSongs())
            return;

        source.loop = false;
        source.Stop();
        BuildOrder(null);
        PlayCurrent();
    }

    void Update()
    {
        if (!playing || AudioSettings.dspTime < nextAt)
            return;

        AudioClip finished = order[index];
        index++;
        if (index >= order.Count)
            BuildOrder(finished);
        PlayCurrent();
    }

    bool HasSongs()
    {
        for (int i = 0; i < songs.Count; i++)
        {
            if (songs[i] != null)
                return true;
        }

        return false;
    }

    void PlayCurrent()
    {
        AudioClip clip = order[index];
        source.clip = clip;
        source.Play();
        playing = true;
        nextAt = AudioSettings.dspTime + clip.length;
    }

    void BuildOrder(AudioClip avoidFirst)
    {
        order.Clear();
        for (int i = 0; i < songs.Count; i++)
        {
            if (songs[i] != null)
                order.Add(songs[i]);
        }

        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        if (order.Count > 1 && order[0] == avoidFirst)
        {
            int swap = Random.Range(1, order.Count);
            (order[0], order[swap]) = (order[swap], order[0]);
        }

        index = 0;
    }
}
