using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Outpost.Enemy;
using Outpost.Player;
using Outpost.UI;
public static class TargetQuotaChecks
{
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static async Task Until(Func<bool> ready,string message)
    {
        float deadline=Time.realtimeSinceStartup+25;
        while(!ready()){Check(Application.isPlaying&&Time.realtimeSinceStartup<deadline,message);await Task.Delay(20);}
    }
    static async Task LoadGame()
    {
        Time.timeScale=1;var load=SceneManager.LoadSceneAsync(GameMenu.GameScene);
        await Until(()=>load.isDone&&PlayerHealth.Instance!=null&&WaveDirector.Instance!=null,"Gameplay load timed out.");
        await Task.Delay(200);WaveDirector.Instance.StopAllCoroutines();
    }
    static async Task Settle(){await Task.Delay(400);Check(Application.isPlaying&&Time.timeScale>0,"Check interrupted.");}
    static void Heard(EnemyBrain brain,Vector3 source)=>typeof(EnemyBrain).GetMethod("OnNoise",Hidden).Invoke(brain,new object[]{source,1000f});
    static int Pursuers(IEnumerable<EnemyBrain> brains)=>brains.Count(b=>b!=null&&b.enabled&&!b.GetComponent<EnemyHealth>().IsDead&&b.DebugTarget=="Player");
    static EnemyBrain Spawn(GameObject prefab,Vector3 position,List<EnemyConfig> configs,bool stationary=true)
    {
        var actor=EnemyDebugSpawner.SpawnAt(prefab,position);Check(actor!=null,"Quota fixture spawn failed.");
        var brain=actor.GetComponent<EnemyBrain>();var config=UnityEngine.Object.Instantiate(brain.config);configs.Add(config);
        if(stationary){config.walkSpeed=0;config.runSpeed=0;config.attackDamage=0;}
        config.canStagger=false;config.screamOnSpawn=false;config.sightRadius=22;config.sightFov=360;
        brain.config=config;return brain;
    }
    public static async Task<object> Main()
    {
        Check(Application.isPlaying,"Enter Play Mode first.");var configs=new List<EnemyConfig>();
        try
        {
            await LoadGame();Check(GeneratorHealth.All.Single().Health==1000,"Generator must start at 1000 HP.");
            var controller=PlayerHealth.Instance.GetComponent<FpsController>();controller.enabled=false;
            var arena=new Vector3(-60,.3f,-4.24f);controller.Teleport(arena,90);
            var catalog=WaveDirector.Instance.catalog;
            var normal=catalog.variants.First(v=>v.id=="walker").prefab;
            var boss=catalog.variants.First(v=>v.id==WaveDirector.Instance.bossVariantId).prefab;
            var actors=new List<EnemyBrain>();
            for(int i=0;i<8;i++){var brain=Spawn(i==7?boss:normal,arena+Vector3.right*(8+i*2),configs);actors.Add(brain);Heard(brain,arena);}
            await Settle();
            Check(Pursuers(actors)==4&&actors.Take(4).All(b=>b.DebugTarget=="Player"),"Eight zombies must initially allocate four nearest simultaneous contacts.");
            actors[7].GetComponent<EnemyHealth>().TakeDamage(1,actors[7].transform.position,Vector3.forward);await Settle();
            Check(actors[7].DebugTarget=="Generator"&&actors.Take(4).All(b=>b.DebugTarget=="Player"),"A new hit stole an occupied FIFO slot.");
            actors[0].GetComponent<EnemyHealth>().Kill();await Settle();
            Check(Pursuers(actors)==4&&actors[4].DebugTarget=="Player"&&actors[7].DebugTarget=="Generator","Oldest waiting contact did not inherit the free slot.");
            actors[1].GetComponent<EnemyHealth>().Kill();await Settle();Check(Pursuers(actors)==3,"Quota did not shrink with six living zombies.");
            actors[2].GetComponent<EnemyHealth>().Kill();await Settle();Check(Pursuers(actors)==3&&actors[5].DebugTarget=="Player","Dynamic odd-count quota or FIFO failed.");
            for(int i=8;i<10;i++){var brain=Spawn(normal,arena+Vector3.right*(8+i*2),configs);actors.Add(brain);Heard(brain,arena);}
            await Settle();Check(Pursuers(actors)==4&&actors[6].DebugTarget=="Player"&&actors[8].DebugTarget=="Generator","New spawns did not expand quota for the oldest queued zombie.");
            var close=new[]{actors[3],actors[4],actors[7],actors[8],actors[9]};
            for(int i=0;i<close.Length;i++)
            {
                float angle=i*Mathf.PI*2/close.Length;var offset=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*1.4f;
                Check(close[i].GetComponent<NavMeshAgent>().Warp(arena+offset),"Close-threat warp failed.");
            }
            Physics.SyncTransforms();await Settle();
            Check(close.All(b=>b.DebugTarget=="Player")&&Pursuers(actors)==5,"Walking-to-generator zombies did not override a full quota at melee range.");
            controller.Teleport(arena+Vector3.back*12,90);Physics.SyncTransforms();await Settle();
            Check(Pursuers(actors)==4&&actors.Skip(3).Take(4).All(b=>b.DebugTarget=="Player")&&actors.Skip(7).All(b=>b.DebugTarget=="Generator"),"Retreat did not restore FIFO slots and generator targets: "+string.Join(", ",actors.Skip(3).Select(b=>b.DebugTarget+"/"+typeof(EnemyBrain).GetField("playerRequestOrder",Hidden).GetValue(b))));

            await LoadGame();controller=PlayerHealth.Instance.GetComponent<FpsController>();controller.enabled=false;
            controller.Teleport(arena+Vector3.forward*100,90);actors.Clear();
            for(int i=0;i<8;i++)actors.Add(Spawn(i==7?boss:normal,arena+Vector3.right*(8+i*2),configs));
            await Settle();Check(Pursuers(actors)==0,"Unaware zombies chase a distant player.");
            var shot=actors[7];shot.GetComponent<EnemyHealth>().TakeDamage(1,shot.transform.position,Vector3.forward);await Settle();
            var alerted=actors.Where(b=>b!=shot&&b.DebugTarget=="Player").ToArray();
            Check(shot.DebugTarget=="Player"&&Pursuers(actors)==4&&alerted.Length==3,"Local hit alert did not recruit nearby zombies into free slots.");
            Check(alerted.All(b=>(b.transform.position-shot.transform.position).sqrMagnitude<=64.1f)&&!actors[0].IsAware,"Hit alert spread beyond its 8m radius.");
            shot.enabled=false;await Settle();
            int eligible=actors.Count(b=>b!=shot&&(bool)typeof(EnemyBrain).GetField("wantsPlayer",Hidden).GetValue(b));
            Check(shot.DebugTarget=="None"&&Pursuers(actors)==Mathf.Min(4,eligible),"Disabled AI retained a slot or blocked the queue.");
            foreach(var health in EnemyHealth.Alive.ToArray())health.Kill();await Settle();

            var generator=GeneratorHealth.All.Single();
            var committed=Spawn(normal,generator.AttackPoints[0].position,configs,false);
            await Until(()=>generator.Health<1000,"Zombie did not reach and damage the generator.");
            Heard(committed,controller.transform.position);
            committed.GetComponent<EnemyHealth>().TakeDamage(1,committed.transform.position,Vector3.forward);await Settle();
            Check(committed.DebugTarget=="Generator","Generator attacker was distracted by distant gunfire despite its commitment.");
            controller.Teleport(committed.transform.position+Vector3.forward*1.4f,0);Physics.SyncTransforms();await Settle();
            Check(committed.DebugTarget=="Player","Committed generator attacker did not defend against a nearby player.");
            controller.Teleport(committed.transform.position+Vector3.forward*10,0);Physics.SyncTransforms();Heard(committed,controller.transform.position);await Settle();
            Check(committed.DebugTarget=="Generator","Committed zombie did not return to generator after player retreated, even with a free player slot.");
            await LoadGame();Check(EnemyHealth.Alive.Count==0&&GeneratorHealth.All.Single().Health==1000,"Restart retained enemies or wrong objective health.");
            var result=new{passed=true,normalPlayerFraction=.5f,activeLivingDenominator=true,bossIncluded=true,fifoPreservesExistingPursuers=true,oldestWaitingGetsSlot=true,dynamicSpawnAndDeathQuota=true,oddCountsRoundUp=true,newHitDoesNotStealSlot=true,localHitAlertRadius=8,nearbyHearingRecruitment=true,meleeCanExceedQuota=true,walkingEnemiesReactAtMelee=true,retreatRestoresFifo=true,generatorCommitmentIgnoresDistantGunfire=true,committedMeleeOverride=true,committedReturnsAfterRetreat=true,disabledBrainReleasesSlot=true,restartClean=true,generatorMaxHealth=1000};
            Directory.CreateDirectory("Docs/Integration");File.WriteAllText("Docs/Integration/TargetQuotaChecks.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            var menu=SceneManager.LoadSceneAsync(GameMenu.MenuScene);await Until(()=>menu.isDone,"Menu load timed out.");return result;
        }
        finally {foreach(var config in configs)if(config!=null)UnityEngine.Object.Destroy(config);}
    }
}
