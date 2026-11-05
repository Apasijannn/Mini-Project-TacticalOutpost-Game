using UnityEngine;
using UnityEngine.UI;

namespace MiniMapModular{
	
public class AuxRadarColy : MonoBehaviour
{
	[HideInInspector] public Image AuxIm;
	
	private int ActionMe;
	private int DetectMe;
	[HideInInspector] public float RadarPersecutor;
	
	[HideInInspector] public float TimeAddT;
	private float TimeCount;
	
	void OnDisable(){
	 AuxIm.color = new Color(AuxIm.color.r, AuxIm.color.g, AuxIm.color.b, 0f);
	}
	
	void Update(){
	 if(ActionMe == 1){
	  if(DetectMe == 0){
	   OffColMy();
	  }else{
	   OnColMy();
	  }
	 }
	}
	
	void OnColMy(){
	 if(TimeCount < 1f){
	  TimeCount += Time.deltaTime;
	   
	  AuxIm.color = new Color(AuxIm.color.r, AuxIm.color.g, AuxIm.color.b, TimeCount/0.25f);
	 }else{
	  ActionMe = 0;
	 }
	}
	
	void OffColMy(){
	 if(TimeCount > 0f){
	  TimeCount -= Time.deltaTime;
	  
	  if(TimeCount <= 1f){
	   AuxIm.color = new Color(AuxIm.color.r, AuxIm.color.g, AuxIm.color.b, TimeCount/1);
	  }
	 }else{
	  ActionMe = 0;
	 }
	}
	
    void OnTriggerEnter2D(Collider2D col){
     if(col.gameObject.name == "Radar" && DetectMe == 0){
	  if(TimeCount > 1){
	   TimeCount = 1f;  
	  }
	  
	  if(RadarPersecutor == 0){
	   AuxIm.gameObject.GetComponent<RectTransform>().localPosition = transform.localPosition;
	   AuxIm.gameObject.GetComponent<RectTransform>().eulerAngles = transform.eulerAngles;
	  }
	  
	  DetectMe = 1;
	  
	  ActionMe = 1;
	 }
    }
	
	void OnTriggerStay2D(Collider2D col){
     if(col.gameObject.name == "Radar" && DetectMe == 0){
	  if(TimeCount > 1){
	   TimeCount = 1f;  
	  }
	  
	  if(RadarPersecutor == 0){
	   AuxIm.gameObject.GetComponent<RectTransform>().localPosition = transform.localPosition;
	   AuxIm.gameObject.GetComponent<RectTransform>().eulerAngles = transform.eulerAngles;
	  }
	  
	  DetectMe = 1;
	  
	  ActionMe = 1;
	 }
    }
	
	void OnTriggerExit2D(Collider2D col){
     if(col.gameObject.name == "Radar" && DetectMe == 1){
	  if(AuxIm.color != new Color(AuxIm.color.r, AuxIm.color.g, AuxIm.color.b, 255f)){
	   AuxIm.color = new Color(AuxIm.color.r, AuxIm.color.g, AuxIm.color.b, 255f);
	  }
	  
	  TimeCount += TimeAddT;
	  
	  DetectMe = 0;
	  
	  ActionMe = 1;
	 }
    }
}

}
