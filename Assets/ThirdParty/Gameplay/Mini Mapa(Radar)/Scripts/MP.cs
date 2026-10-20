using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MiniMapModular{
		
public class MP : MonoBehaviour
{	
	[Space]
	public Custom_MP MS;
	
	[Header("Player")]
	public Transform Player_RY;
	
	[Header("CamMM&Icon")]
	public Camera Cam_Map;
	public LayerMask LayerSelect;
	public Vector3 PosyMeLocal = new Vector3(0f, 24.5f, 0f);
	[Range(0f,1f)] public int CamFixed;
	[Range(0.01f,1f)] public float SmothCam = 0.1f;
	[Range(30f,1000f)] public float Aux_ClipPlane_Far = 50;
	[Range(25f,105f)] public float CamSize = 35f;
	[Range(20f,100f)] public float Min_Dis_Icon = 30f;
	[Range(0.1f,1f)] public float IconScale = 1f;
	[Tooltip("Used to further enlarge the specific central icon to make it larger if you prefer to differentiate it from the others.")]
	[Range(0f,1f)] public float SizeIconAdd = 0;
	[Range(-1f,1f)] public float mult = -0.17f;
	[HideInInspector] public  Transform Cam_MapT;
	
	[Header("TextAngle")]
	[Range(0f,1f)] public int AngleText;
	public Color AngleText_C;
	public Color SpriteTex_C;
	
	[Header("Custom_MM")]
	public Vector2 MM_Posy = new Vector2(750f, -350f);
	[Range(0.1f,10f)] public float MMSize = 1f;
	public Sprite BorderLine_Icon;
	public Color BorderLine_C;
	public Sprite Fund_Icon;
	public Color Fund_C;
	public Sprite Central_Icon;
	public Color Central_C;
	private RectTransform CentralI;
	[HideInInspector] public Transform MPT;
	[HideInInspector] public Transform MPT1;
	[HideInInspector] public Transform MPT2;
	
	[Header("Ray_Sys")]
	[Range(0f,1f)] public int Ray_On;
	public Sprite Ray_Icon;
	public Color Ray_C;
	private GameObject  Rays;
	
	[Header("Direction_Sys")]
	[Range(0f,1f)] public int Direction_On;
	[Range(0.1f,5f)] public float Dir_Scale = 1f;
	public Sprite Direction_Icon;
	public Color Direction_C;
	private GameObject  Directions;
	
	[Header("Coordinates_Sys")]
	[Range(0f,1f)] public int Coordinates_On;
	public Sprite Coordinates_Icon;
	public Color Coordinates_C;
	private GameObject  Coordinates;
	
	[Header("Radar_Sys")]
	[Range(0f,1f)] public int Radar_On;
	[Range(0f,2f)] public int Radar_Type;
	[Range(0f,1f)] public int RadarPersecutor;
	[Range(0.1f,5f)] public float TimeViewOff = 1.5f;
	[Range(0.1f,5f)] public float SizeRad1 = 0.1f;
	[Range(1f,999f)] public float SpeedRad1 = 150f;
	[Range(0.1f,10f)] public float SpeedRad2and3 = 2f;
	public Color Radar_C;
	
	private Transform RotMM;
	
	void OnValidate(){
	 if(Cam_Map != null){
	  if(Cam_MapT == null){
	   Cam_MapT = Cam_Map.gameObject.transform;
	  }else{
	   if(Cam_MapT.localPosition != PosyMeLocal){
        Cam_MapT.localPosition = PosyMeLocal;
	   }
	   
	   if(Cam_MapT.rotation != Quaternion.Euler(90f, 0f, 0f)){
        Cam_MapT.rotation = Quaternion.Euler(90f, 0f, 0f);
	   }
	  }
	 
	  if(Cam_Map.cullingMask != LayerSelect){
	   Cam_Map.cullingMask = LayerSelect;
	  }
	  
	  if(Cam_Map.farClipPlane != Aux_ClipPlane_Far){
	   Cam_Map.farClipPlane = Aux_ClipPlane_Far;
	  }
	  
	  if(Cam_Map.orthographicSize  != CamSize){
	   Cam_Map.orthographicSize  = CamSize;
	  }  
	 }
	 
	 if(MS != null){
	  CentralI = MS.Central_Ima.gameObject.GetComponent<RectTransform>();
	  Directions = MS.DirectionV.gameObject;
	  Rays = MS.Ray_Ima.gameObject;
	  Coordinates = MS.Coordinates_Ima.gameObject;
	  RotMM = MS.RotMM;
	  
	  MPT1 = MS.MPT1;
	  MPT2 = MS.MPT2;
	  MPT = MS.MPT;
	 }
	 
	 if(MPT != null){
	  if(MS.Map_Status.localScale != new Vector3(MMSize, MMSize, 1f)){
	   MS.Map_Status.localScale = new Vector3(MMSize, MMSize, 1f);
	  }
	  
	  if(MS.Map_Status.localPosition != new Vector3(MM_Posy.x, MM_Posy.y, 0f)){
	   MS.Map_Status.localPosition = new Vector3(MM_Posy.x, MM_Posy.y, 0f);
	  }
	  
	  if(CentralI.localScale != new Vector3((IconScale + SizeIconAdd), (IconScale + SizeIconAdd), 1f)){
	   CentralI.localScale = new Vector3((IconScale + SizeIconAdd), (IconScale + SizeIconAdd), 1f);	  
	  }
	  
	  if(Direction_On == 1){
	   if(Directions.GetComponent<RectTransform>().localScale != new Vector3(Dir_Scale, 1, 1f)){
	    Directions.GetComponent<RectTransform>().localScale = new Vector3(Dir_Scale, 1, 1f);
	   }
	  
	   if(Directions.activeSelf == false){
	    Directions.SetActive(true);
	   }
	  }else{
	   if(Directions.activeSelf == true){
	    Directions.SetActive(false);
	   }
	  }
	  //
	  if(AngleText == 0 && MS.AngleTextG.activeSelf == true){
	   MS.AngleTextG.SetActive(false);
	  }
	  
	  if(AngleText == 1 && MS.AngleTextG.activeSelf == false){
	   MS.AngleTextG.SetActive(true);
	  }
	  
	  if(Rays.activeSelf == false && Ray_On == 1){
	   Rays.SetActive(true);
	  }
	 
	  if(Rays.activeSelf == true && Ray_On == 0){
	   Rays.SetActive(false);
	  }
	  //I
	  if(BorderLine_Icon != null){
	   MS.BorderLine_Ima.sprite = BorderLine_Icon;
	  }
	 
	  if(Fund_Icon != null){
	   MS.Fund_Ima.sprite = Fund_Icon;
	  }
	 
	  if(Central_Icon != null){
	   MS.Central_Ima.sprite = Central_Icon;
	  }
	 
	  if(Coordinates_Icon != null){
	   MS.Coordinates_Ima.sprite = Coordinates_Icon;
	  }
	 
	  if(Ray_Icon != null){
	   MS.Ray_Ima.sprite = Ray_Icon;
	  }
	 
	  if(Direction_Icon != null){
	   MS.Direction_Ima.sprite = Direction_Icon;
	  }
	  //C
	  if(AngleText_C != new Color32(0,0,0,0) && MS.AngleRot.color != AngleText_C){
	   MS.AngleRot.color = AngleText_C;
	  }
	  
	  if(SpriteTex_C != new Color32(0,0,0,0) && MS.SpriteTex_Ima.color != SpriteTex_C){
	   MS.SpriteTex_Ima.color = SpriteTex_C;
	  }
	  
	  if(BorderLine_C != new Color32(0,0,0,0) && MS.BorderLine_Ima.color != BorderLine_C){
	   MS.BorderLine_Ima.color = BorderLine_C;
	  }
	 
	  if(Fund_C != new Color32(0,0,0,0) && MS.Fund_Ima.color != Fund_C){
	   MS.Fund_Ima.color = Fund_C;
	  }
	 
	  if(Central_C != new Color32(0,0,0,0) && MS.Central_Ima.color != Central_C){
	   MS.Central_Ima.color = Central_C;
	  }
	 
	  if(Coordinates_C != new Color32(0,0,0,0) && MS.Coordinates_Ima.color != Coordinates_C){
	   MS.Coordinates_Ima.color = Coordinates_C;
	  }
	 
	  if(Ray_C != new Color32(0,0,0,0) && MS.Ray_Ima.color != Ray_C){
	   MS.Ray_Ima.color = Ray_C;
	  }
	 
	  if(Direction_C != new Color32(0,0,0,0) && MS.Direction_Ima.color != Direction_C){
	   MS.Direction_Ima.color = Direction_C;
	  }
	  //
	  if(Coordinates.activeSelf == false && Coordinates_On == 1){
	   Coordinates.SetActive(true);
	  }
	 
	  if(Coordinates.activeSelf == true && Coordinates_On == 0){
	   Coordinates.SetActive(false);
	  }
	  //
	  if(Radar_On == 1){
	   if(Radar_Type == 0){
		if(MS.Type1.activeSelf != true){
	     MS.Type1.SetActive(true);
	     MS.Type2.SetActive(false);
	     MS.Type3.SetActive(false);
		
		 MS.RadarT.localScale = new Vector3(1f, 1f, 1f);
	    }
		
		if(MS.Type1.GetComponent<RectTransform>().localScale.x != SizeRad1){
		 MS.Type1.GetComponent<RectTransform>().localScale = new Vector3(SizeRad1, 1f, 1f);
		}
		
		if(Radar_C != new Color32(0,0,0,0) && MS.Type1.GetComponent<Image>().color != Radar_C){
		 MS.Type1.GetComponent<Image>().color = Radar_C;
		}
	   }
	   
	   if(Radar_Type == 1){
		if(MS.Type2.activeSelf != true){
	     MS.Type1.SetActive(false);
		 MS.Type2.SetActive(true);
		 MS.Type3.SetActive(false);
		
		 MS.RadarT.eulerAngles = new Vector3(0f, 0f, 0f);
		 
		 MS.RadarT.localScale = new Vector3(0.5f, 0.5f, 1f);
	    }
		
		if(Radar_C != new Color32(0,0,0,0) && MS.Type2.GetComponent<Image>().color != Radar_C){
		 MS.Type2.GetComponent<Image>().color = Radar_C;
		}
	   }
	   
	   if(Radar_Type == 2){
		if(MS.Type3.activeSelf != true){
	     MS.Type1.SetActive(false);
		 MS.Type2.SetActive(false);
		 MS.Type3.SetActive(true);
		 
		 MS.RadarT.localScale = new Vector3(0.5f, 0.5f, 1f);
		}
		
		if(Radar_C != new Color32(0,0,0,0) && MS.Type3.GetComponent<Image>().color != Radar_C){
		 MS.Type3.GetComponent<Image>().color = Radar_C;
		}
	   }
	  }else{
	   if(MS.Type1.activeSelf == true){
	    MS.Type1.SetActive(false);
	   }
	   
	   if(MS.Type2.activeSelf == true){
	    MS.Type2.SetActive(false);
	   }
	   
	   if(MS.Type3.activeSelf == true){
	    MS.Type3.SetActive(false);
	   }
	  }
	 }
	}
	
	void Awake(){
	 if(Radar_Type == 0){
	  MS.RadarT.transform.SetParent(MS.CoordRadRot);
	 }

     if(Radar_Type > 0){
	  MS.RadarT.transform.SetParent(MS.RadarFixed);
	 }
	 
	 if(Cam_MapT.parent == null && CamFixed == 0){
	  Cam_MapT.SetParent(Player_RY);
	 }
	 
	 if(Cam_MapT.parent != null && CamFixed == 1){
	  Cam_MapT.SetParent(null); 
	 }
	}
	
	void Update(){
	 if(AngleText == 1){
	  int AuxRT = (int)Player_RY.eulerAngles.y;
	  
	  MS.AngleRot.text = AuxRT.ToString() + "°";
	 }
	 
	 if(Coordinates_On == 1 && CamFixed == 0){
      RotMM.eulerAngles = new Vector3(0f, 0f, Player_RY.eulerAngles.y);
	 }
	 
	 if(CamFixed == 1){
	  MS.IconPlayerR.eulerAngles = new Vector3(0f, 0f, -Player_RY.eulerAngles.y);
	 }
	 
	 if(Radar_On == 1){
	  if(Radar_Type == 0){
	   if(CamFixed == 0){
	    RotMM.eulerAngles = new Vector3(0f, 0f, Player_RY.eulerAngles.y);
	   }
	   
	   MS.RadarT.eulerAngles -= new Vector3(0f, 0f, SpeedRad1 * Time.deltaTime);
	  }else{
	   Vai:
	   
	   if(MS.RadarT.localScale.x <= 1f){
	    MS.RadarT.localScale = Vector3.Lerp(MS.RadarT.localScale, new Vector3(1.15f, 1.15f, 1f), SpeedRad2and3 * Time.deltaTime);
	   }else{
		MS.RadarT.localScale = new Vector3(0f, 0f, 1f);

	    goto Vai;
	   }
	  }
	 }
    }

	void FixedUpdate(){
	 if(CamFixed == 1){
	  Cam_MapT.position = Vector3.Lerp(Cam_MapT.position, Player_RY.position + PosyMeLocal, SmothCam);
	 }
    }
}

}
