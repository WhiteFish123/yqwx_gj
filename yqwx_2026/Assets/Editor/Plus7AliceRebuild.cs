// 一次性工具：把 Assets/Art/plus7（alice animation 里的新动作图接进爱丽丝敌人。
//
// 做两件事：
//   ① 所有这些 PNG 统一改成：Sprite(Single)、整张图、pivot 居中、PPU 16、Point 过滤、不压缩
//   ② 用新图重建 Assets/Animations/Enemy/Alice 下的 7 个片段（名字不变，所以 Enemy_Alice 不用改）
//
// 菜单：Tools/爱丽丝/重建 plus7 动画（也可以用右边的静态方法 Run()）
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class Plus7AliceRebuild
{
    private const string ArtRoot = "Assets/Art/plus7（alice animation";
    private const string ClipRoot = "Assets/Animations/Enemy/Alice";
    private const float PixelsPerUnit = 16f;

    // 文件夹 → 片段名、帧率、是否循环
    private class Job
    {
        public string folder;
        public string clip;
        public float fps;
        public bool loop;
    }

    private static readonly Job[] Jobs =
    {
        new Job { folder = "walk down_often", clip = "Alice_Idle_Front", fps = 5f, loop = true },
        new Job { folder = "Walk up", clip = "Alice_Idle_Back", fps = 5f, loop = true },
        new Job { folder = "Walk left_right", clip = "Alice_Idle_Side", fps = 5f, loop = true },
        new Job { folder = "charge down", clip = "Alice_Charge_Front", fps = 20f, loop = false },
        new Job { folder = "Charge up", clip = "Alice_Charge_Back", fps = 20f, loop = false },
        new Job { folder = "charge left_right", clip = "Alice_Charge_Side", fps = 20f, loop = false },
        new Job { folder = "Blast", clip = "Alice_Crash", fps = 12f, loop = false },
    };

    [MenuItem("Tools/爱丽丝/重建 plus7 动画")]
    public static void RunMenu()
    {
        Debug.Log(Run());
    }

    public static string Run()
    {
        List<string> report = new List<string>();

        int importCount = ConfigureImages();
        report.Add("images=" + importCount);

        foreach (Job job in Jobs)
        {
            string result = RebuildClip(job);
            report.Add(result);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return string.Join("\n", report.ToArray());
    }

    // ① 统一导入设置
    private static int ConfigureImages()
    {
        string[] files = Directory.GetFiles(ArtRoot, "*.png", SearchOption.AllDirectories);
        int count = 0;

        foreach (string file in files)
        {
            string path = file.Replace('\\', '/');

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null)
                continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteBorder = Vector4.zero;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
            count++;
        }

        return count;
    }

    // ② 用新图重建片段
    private static string RebuildClip(Job job)
    {
        string folder = Path.Combine(ArtRoot, job.folder);
        List<Sprite> sprites = LoadSprites(folder);

        if (sprites.Count == 0)
            return job.clip + ": no sprites in " + job.folder;

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipRoot + "/" + job.clip + ".anim");

        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, ClipRoot + "/" + job.clip + ".anim");
        }

        clip.frameRate = job.fps;
        clip.wrapMode = job.loop ? WrapMode.Loop : WrapMode.Clamp;

        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = string.Empty,
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[sprites.Count];

        for (int i = 0; i < sprites.Count; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / job.fps,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = job.loop;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        return job.clip + ": frames=" + sprites.Count + " fps=" + job.fps + " loop=" + job.loop + " first=" + sprites[0].name;
    }

    // 按文件名自然排序（1,2,3... 或 a,b,c...）
    private static List<Sprite> LoadSprites(string folder)
    {
        List<Sprite> sprites = new List<Sprite>();

        if (!Directory.Exists(folder))
            return sprites;

        string[] files = Directory.GetFiles(folder, "*.png");
        List<string> paths = new List<string>(files);

        paths.Sort(CompareNatural);

        foreach (string file in paths)
        {
            string path = file.Replace('\\', '/');
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite != null)
                sprites.Add(sprite);
        }

        return sprites;
    }

    private static int CompareNatural(string a, string b)
    {
        string na = Path.GetFileNameWithoutExtension(a);
        string nb = Path.GetFileNameWithoutExtension(b);

        int ia, ib;
        bool aNum = int.TryParse(na, out ia);
        bool bNum = int.TryParse(nb, out ib);

        if (aNum && bNum)
            return ia.CompareTo(ib);

        return string.CompareOrdinal(na, nb);
    }
}
