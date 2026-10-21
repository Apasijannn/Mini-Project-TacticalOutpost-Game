using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Outpost;
using Outpost.UI;
public static class SetupExplosionAudio
{
    public const string Root="Assets/TacticalOutpost/Gameplay/Audio/Explosions";
    public static object Main()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        var scene=SceneManager.GetSceneByPath(GameMenu.GameScene);
        if(!scene.IsValid()||!scene.isLoaded||scene.isDirty)throw new InvalidOperationException("Open a clean gameplay scene first.");
        var generator=Mix("Generator blast",new[]{("massive-explosion-2-397983",.7f,.94f,0f,0f),("nuclear-explosion-386181",.55f,.9f,.06f,220f)});
        var building=Mix("Building blast",new[]{("loud-explosion-sound-425458",.65f,.91f,0f,0f),("massive-explosion-2-397983",.25f,.86f,.09f,900f)});
        var tanks=Mix("Tank group blast",new[]{("explosion-sound-effect-425455",.7f,1.06f,0f,0f),("explosion-fx-425453",.2f,1.12f,.045f,0f)});
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var destruction=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GeneratorDestruction>(true)).Single();
        Undo.RecordObject(destruction,"Add cinematic explosion sounds");
        destruction.generatorBlastSound=Clip("Generator blast");destruction.buildingBlastSound=Clip("Building blast");destruction.tankBlastSound=Clip("Tank group blast");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        var preview=new float[48000*12];
        foreach(var layer in new[]{(generator,.55f,1.2f),(building,.4f,1.7f),(tanks,.25f,2.2f)})
            for(int i=0;i<layer.Item1.Length&&i+(int)(layer.Item3*48000)<preview.Length;i++)preview[i+(int)(layer.Item3*48000)]+=layer.Item1[i]*layer.Item2;
        Wave("Temp/ExplosionMixPreview.wav",preview);
        string note="User-provided Dragon Studio MP3s from C:/CoolYeah/SEM5/game-pinter/game-projects/xplosion. Original names preserved in Sources. Generator: Massive Explosion + low-pass Nuclear Explosion. Building: Loud Explosion Sound + filtered Massive Explosion. Tanks: Explosion Sound Effect + Explosion FX; one group sound, not four full-volume duplicates. Derived mono WAVs peak at 0.7, with short edge fades. Runtime gains: 0.55 / 0.4 / 0.25; spatial blend 0.65. No license terms inferred from filenames; retain the source download records with project documentation.";
        File.WriteAllText(Root+"/README.txt",note);
        var result=new{generatorSeconds=generator.Length/48000f,buildingSeconds=building.Length/48000f,tanksSeconds=tanks.Length/48000f,previewPeak=preview.Max(v=>Mathf.Abs(v)),sceneSaved=true};
        Directory.CreateDirectory("Docs/Integration");File.WriteAllText("Docs/Integration/ExplosionAudioMix.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
        return result;
    }
    static AudioClip Clip(string name)
    {
        string path=Root+"/"+name+".wav";var importer=(AudioImporter)AssetImporter.GetAtPath(path);
        var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;
        importer.forceToMono=false;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    static float[] Mix(string name,(string source,float gain,float pitch,float delay,float cutoff)[] layers)
    {
        var output=new float[48000*10];int length=0;
        foreach(var layer in layers)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Sources/dragon-studio-"+layer.source+".mp3");
            if(clip==null)throw new InvalidOperationException("Missing source: "+layer.source);
            var data=new float[clip.samples*clip.channels];if(!clip.GetData(data,0))throw new InvalidOperationException("Cannot decode source.");
            int frames=Mathf.CeilToInt(clip.length/layer.pitch*48000),offset=Mathf.RoundToInt(layer.delay*48000);
            length=Mathf.Max(length,frames+offset);float filtered=0,a=1-Mathf.Exp(-2*Mathf.PI*layer.cutoff/48000);
            for(int i=0;i<frames;i++)
            {
                float position=i*layer.pitch*clip.frequency/48000;int frame=Mathf.Min((int)position,clip.samples-1),next=Mathf.Min(frame+1,clip.samples-1);float sample=0;
                for(int c=0;c<clip.channels;c++)sample+=Mathf.Lerp(data[frame*clip.channels+c],data[next*clip.channels+c],position-frame)/clip.channels;
                if(layer.cutoff>0){filtered+=a*(sample-filtered);sample=filtered;}
                output[i+offset]+=sample*layer.gain;
            }
        }
        Array.Resize(ref output,length);float peak=output.Max(v=>Mathf.Abs(v));
        if(peak<=0)throw new InvalidOperationException("Silent mix.");
        for(int i=0;i<length;i++)output[i]*=.7f/peak*Mathf.Min(1,i/144f)*Mathf.Min(1,(length-1-i)/5760f);
        Wave(Root+"/"+name+".wav",output);return output;
    }
    static void Wave(string path,float[] data)
    {
        using(var writer=new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+data.Length*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(48000);writer.Write(96000);writer.Write((short)2);writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(data.Length*2);
            foreach(float sample in data)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample,-1,1)*32767));
        }
    }
    public static object Analyze()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        const string backup="C:/Users/Faiz/OneDrive/Documents/TacticalOutpostBackups/20261021_ExplosionAudio/Before";
        Directory.CreateDirectory(backup);
        foreach(var path in new[]{"Assets/Scenes/Gameplay/TacticalOutpost_Game.unity","Assets/TacticalOutpost/Gameplay/Scripts/GeneratorDestruction.cs","Tools/Integration/BlackoutEndingChecks.cs","GAMEPLAY_PRESENTATION_BRIEF.md"})
            if(!File.Exists(backup+"/"+Path.GetFileName(path)))File.Copy(path,backup+"/"+Path.GetFileName(path));
        Directory.CreateDirectory(Root+"/Sources");
        foreach(var path in Directory.GetFiles("C:/CoolYeah/SEM5/game-pinter/game-projects/xplosion","*.mp3"))
            if(!File.Exists(Root+"/Sources/"+Path.GetFileName(path)))File.Copy(path,Root+"/Sources/"+Path.GetFileName(path));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        return Directory.GetFiles(Root+"/Sources","*.mp3").Select(path=>
        {
            var importer=(AudioImporter)AssetImporter.GetAtPath(path);
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;
            importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);var data=new float[clip.samples*clip.channels];
            if(!clip.GetData(data,0))throw new InvalidOperationException("Cannot decode "+path);
            float peak=data.Max(v=>Mathf.Abs(v));
            var quarters=Enumerable.Range(0,Mathf.CeilToInt(clip.length*4)).Select(q=>
            {int start=q*clip.frequency*clip.channels/4,end=Mathf.Min(data.Length,(q+1)*clip.frequency*clip.channels/4);double energy=0;for(int i=start;i<end;i++)energy+=data[i]*data[i];return Math.Round(Math.Sqrt(energy/Math.Max(1,end-start)),3);}).ToArray();
            return new{clip.name,clip.length,clip.channels,clip.frequency,peak,rmsEveryQuarterSecond=quarters};
        }).ToArray();
    }
}
