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

    [SerializeField] private string defaultMusicId;//启动时自动播放的音乐 id，留空则不播

    [Range(0f, 1f)] [SerializeField] private float musicVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    //Inspector列表查表字典:id->clip
    private readonly Dictionary<string, AudioClip> musicMap = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, AudioClip> sfxMap = new Dictionary<string, AudioClip>();

    private AudioSource musicSource;//音乐
    private AudioSource sfxSource;//音效
    private string currentMusicId;

    //循环音效：每个 id 一个独立音源，才能"单独开始/单独停"
    private readonly Dictionary<string, AudioSource> loopSources = new Dictionary<string, AudioSource>();
    private readonly Dictionary<string, float> loopScales = new Dictionary<string, float>();

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
        set
        {
            sfxVolume = Mathf.Clamp01(value);
            RefreshLoopVolumes();
        }
    }

    //当前正在播的音乐 id（没在播就是空）
    public string CurrentMusicId
    {
        get { return currentMusicId; }
    }

    public bool IsMusicPlaying
    {
        get { return musicSource != null && musicSource.isPlaying; }
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

    private void Start()//启动时自动播放默认背景音乐
    {
        if (string.IsNullOrEmpty(defaultMusicId))
            return;

        PlayMusic(defaultMusicId);
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

    public void PauseMusic()//暂停背景音乐（保留进度，ResumeMusic 能接着播）
    {
        if (musicSource != null)
            musicSource.Pause();
    }

    public void ResumeMusic()//继续播放被暂停的背景音乐
    {
        if (musicSource != null)
            musicSource.UnPause();
    }

    // ===== 循环音效（燃烧声、走路声这种"持续响"的）=====

    //开始循环播放。同一个 id 已经在播就不会重播，只更新音量。
    public void PlayLoopSfx(string id, float volumeScale = 1f)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[AudioManager] PlayLoopSfx 的 id 为空");
            return;
        }

        AudioClip clip;
        if (!sfxMap.TryGetValue(id, out clip))
        {
            Debug.LogWarning("[AudioManager] 未找到循环音效：" + id);
            return;
        }

        AudioSource source = GetLoopSource(id);

        source.clip = clip;
        source.loop = true;
        loopScales[id] = Mathf.Clamp01(volumeScale);
        source.volume = loopScales[id] * sfxVolume;

        if (!source.isPlaying)
            source.Play();
    }

    //立刻停止某个循环音效（燃烧熄灭、停下脚步时调它）
    public void StopLoopSfx(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        AudioSource source;
        if (loopSources.TryGetValue(id, out source) && source != null)
            source.Stop();
    }

    //停掉所有循环音效（切场景、暂停时收尾用）
    public void StopAllLoopSfx()
    {
        foreach (KeyValuePair<string, AudioSource> pair in loopSources)
        {
            if (pair.Value != null)
                pair.Value.Stop();
        }
    }

    public bool IsLoopSfxPlaying(string id)
    {
        AudioSource source;
        return loopSources.TryGetValue(id, out source) && source != null && source.isPlaying;
    }

    //每个循环音效一个独立 AudioSource（挂在 AudioManager 下的子物体上）
    private AudioSource GetLoopSource(string id)
    {
        AudioSource source;
        if (loopSources.TryGetValue(id, out source) && source != null)
            return source;

        source = GetOrAddAudioSource("Loop_" + id);
        source.loop = true;
        source.playOnAwake = false;
        source.volume = sfxVolume;

        loopSources[id] = source;
        return source;
    }

    //音量改动后，把所有循环音效的音量按各自的倍率重新算一遍
    private void RefreshLoopVolumes()
    {
        foreach (KeyValuePair<string, AudioSource> pair in loopSources)
        {
            if (pair.Value == null)
                continue;

            float scale;
            if (!loopScales.TryGetValue(pair.Key, out scale))
                scale = 1f;

            pair.Value.volume = scale * sfxVolume;
        }
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
