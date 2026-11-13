using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AudioEntry//音频配置
{
    public string id;//代码里播放时用的名字
    public AudioClip clip;//实际音频资源
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    //音乐列表+音效列表
    [SerializeField] private List<AudioEntry> musicList = new List<AudioEntry>();
    [SerializeField] private List<AudioEntry> sfxList = new List<AudioEntry>();

    [Range(0f, 1f)] [SerializeField] private float musicVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    //Inspector列表查表字典:id->clip
    private readonly Dictionary<string, AudioClip> musicMap = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, AudioClip> sfxMap = new Dictionary<string, AudioClip>();

    private AudioSource musicSource;//音乐
    private AudioSource sfxSource;//音效
    private string currentMusicId;

    public float MusicVolume
    {
        get { return musicVolume; }
        set
        {
            musicVolume = Mathf.Clamp01(value);
            if (musicSource != null) musicSource.volume = musicVolume;
        }
    }

    public float SfxVolume
    {
        get { return sfxVolume; }
        set { sfxVolume = Mathf.Clamp01(value); }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildMap(musicList, musicMap, "音乐");
        BuildMap(sfxList, sfxMap, "音效");

        musicSource = GetOrAddAudioSource("MusicSource");
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;

        sfxSource = GetOrAddAudioSource("SfxSource");
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
    }

    
    public void PlayMusic(string id, bool loop = true)//播放背景音乐：同一首正在播就不重播，换歌则切到新音乐
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[AudioManager] PlayMusic 的 id 为空");
            return;
        }

        if (currentMusicId == id && musicSource.isPlaying) return;

        AudioClip clip;
        if (!musicMap.TryGetValue(id, out clip))
        {
            Debug.LogWarning("[AudioManager] 未找到音乐：" + id);
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
        currentMusicId = id;
    }

    public void StopMusic()//停止背景音乐
    {
        musicSource.Stop();
        currentMusicId = null;
    }

    public void PlaySfx(string id, float volumeScale = 1f)//播放音效
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[AudioManager] PlaySfx 的 id 为空");
            return;
        }

        AudioClip clip;
        if (!sfxMap.TryGetValue(id, out clip))
        {
            Debug.LogWarning("[AudioManager] 未找到音效：" + id);
            return;
        }

        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale) * sfxVolume);
    }

    private AudioSource GetOrAddAudioSource(string childName)//找到（或创建）指定名字的子对象上的AudioSource
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            child = go.transform;
        }

        AudioSource source = child.GetComponent<AudioSource>();
        if (source == null) source = child.gameObject.AddComponent<AudioSource>();

        return source;
    }

    //把Inspector列表转成字典
    private static void BuildMap(List<AudioEntry> entries, Dictionary<string, AudioClip> map, string group)
    {
        map.Clear();
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            AudioEntry entry = entries[i];
            if (entry == null || entry.clip == null || string.IsNullOrEmpty(entry.id))
            {
                Debug.LogWarning("[AudioManager]" + group + "列表第 " + i + "项缺少 id 或 clip，已跳过");
                continue;
            }

            if (map.ContainsKey(entry.id))
            {
                Debug.LogWarning("[AudioManager] " + group + "列表存在重复 id：" + entry.id);
                continue;
            }

            map.Add(entry.id, entry.clip);
        }
    }
}
