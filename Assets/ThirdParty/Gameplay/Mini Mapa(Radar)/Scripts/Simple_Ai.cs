using UnityEngine;

namespace MiniMapModular{
	
public class Simple_Ai : MonoBehaviour
{
    [Range(0f,1f)] public int RandomAction = 0;
	private int RandomId = 0;
	public float DistNTarget = 1.5f;
	private float DistTarget;
	public float SpeedMe = 5f;
    public float SpeedRot = 5f;
	public Transform[] Targets;
	private Transform Target;
	private Vector3 DirTarget;
	private Quaternion RotMe;
	
	void Start(){
	 NewTarget();
	}
	
	void NewTarget(){
	 if(RandomAction == 1){
      RandomId = Random.Range(0, Targets.Length);
	 
      Target = Targets[RandomId];
	  
	  return;
	 }else{
	  RandomId += 1;
	   
	  if(RandomId > (Targets.Length - 1)){
	   RandomId = 0;
	  }
	  
	  Target = Targets[RandomId];
	  
	  return;
	 }
    }
	
	void Update(){
	 DirTarget = (Target.position - transform.position).normalized;
	 
     RotMe = Quaternion.LookRotation(DirTarget, Vector3.up);
     transform.rotation = Quaternion.Lerp(transform.rotation, RotMe, Time.deltaTime * SpeedRot);
	  
     transform.position += transform.forward * SpeedMe * Time.deltaTime;

     DistTarget = Vector3.Distance(transform.position, Target.position);
	 
     if(DistTarget <= DistNTarget){
      NewTarget();
     }
    }
}

}