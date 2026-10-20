using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MiniMapModular{
	
public class Aux_MP : MonoBehaviour
{
	private MP MP_Ok;
	
	[Space]
	public GameObject Icon_Arrow, Arrow_Icon, Icon;
	private GameObject Ref_Me, Aux_Icon_A, Aux_IconM,AuxSizeI;
	
	private Transform Target;

	private RectTransform Icon_A, Icon_M;
	
	private Vector2 Aux1Vector1;
	private Vector2 Aux1Vector2;
	
	private Vector3 Vetar;
	
	private Image AuxIm;
	
	private float flip, angle, Distance_Arrow, ajust1 = 2, ajust2 = 2, aux1, aux2;
	
	[Header("Settings")]
	[Space]
	[Range(0f,1f)] public int Arrow = 1;
	[Range(0f,1f)] public int Icon_Dir = 0;
	[Range(0f,1f)] public int HiddenIcon;
	[Range(0f,1f)] public int RotIconMe;
	[Range(0f,2f)] public int LayerIconView;
	[Tooltip("Used to enlarge this specific icon to make it larger than the default size provided in the MP.")]
	[Range(0f,1f)] public float SizeIconAdd = 0;
	
	[Header("Custom")]
	[Space]
	public Sprite Sprite_Me;
	public Color Icon_C;
	public Sprite Sprite_Arrow;
	public Color Arrow_C;
	
	Vector3 TransformToHUDSpaceMap(Vector3 worldSpaceMap){
     var screenSpaceMap = MP_Ok.Cam_Map.WorldToScreenPoint(worldSpaceMap);
     // OUTPOST PATCH: RenderTexture dinaikkan ke 1024px agar peta besar tajam; koordinat ikon dinormalkan ke 256px (kalibrasi asli aset).
     float outpostK = 256f / MP_Ok.Cam_Map.pixelWidth;
     return (screenSpaceMap - new Vector3(MP_Ok.Cam_Map.pixelWidth / ajust1, MP_Ok.Cam_Map.pixelHeight / ajust2)) * outpostK;
    }
	
	void OnValidate(){
	 if(HiddenIcon == 1){
	  if(Arrow == 1){
       Arrow = 0;
	  }
	  
      if(Icon_Dir == 1){
       Icon_Dir = 0;
	  }	  
	 }
	 
     if(Arrow == 1 && Icon_Dir == 1){
      Icon_Dir = 0;
	 }
	 
	 if(Arrow_C == new Color32(0,0,0,0) && Arrow == 1){
	  if(Icon_C != new Color32(0,0,0,0)){
	   Arrow_C = Icon_C;
	  }else{
	   Arrow_C = new Color32(0,0,0,255);
	  }
	 }
    }
	
	void Start(){
	 MP_Ok = GameObject.Find("Cam_Map").GetComponent<MP>();
	 
	 Target = transform;
	 
	 Ref_Me = new GameObject("Empty_Ref");
	 
	 if(LayerIconView == 0){
	  Ref_Me.transform.SetParent(MP_Ok.MPT);
	 }
	 
	 if(LayerIconView == 1){
	  Ref_Me.transform.SetParent(MP_Ok.MPT1);
	 }
	 
	 if(LayerIconView == 2){
	  Ref_Me.transform.SetParent(MP_Ok.MPT2);
	 }
	 
	 Ref_Me.AddComponent<RectTransform>();
	 
	 Ref_Me.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
	 
	 Ref_Me.GetComponent<RectTransform>().localScale = new Vector3(1f, 1f, 1f);
	}
	
	void OnDisable(){
	 Destroy(Ref_Me);
	}
	
	void LateUpdate(){
	 Aux1Vector1 = new Vector2(MP_Ok.Cam_Map.transform.position.x, MP_Ok.Cam_Map.transform.position.z);
	 Aux1Vector2 = new Vector2(Target.position.x, Target.position.z);
	 
	 Distance_Arrow = Vector2.Distance(Aux1Vector1, Aux1Vector2);
	 
	 aux1 = MP_Ok.Cam_Map.transform.position.x - Target.position.x;
	 
	 aux2 = MP_Ok.Cam_Map.transform.position.z - Target.position.z;
	 
	 Vetar = new Vector3(aux1 * MP_Ok.mult, 0f, aux2 * MP_Ok.mult);
	 
	 var TargetPosA = TransformToHUDSpaceMap(Target.position);
	 var TargetPos = TransformToHUDSpaceMap(Target.position + Vetar);
	 angle = Vector2.SignedAngle(Vector2.up, new Vector2(TargetPosA.x, TargetPosA.y));
	 
	 if(Icon_A == null && (Arrow + Icon_Dir) > 0){
	  if(Arrow == 1){
	   Aux_Icon_A = (GameObject) Instantiate(Icon_Arrow, transform.position, transform.rotation);
	   
	   Aux_Icon_A.GetComponent<Aux_Icon>().Local_Icon.sprite = Sprite_Arrow;
	   
	   Aux_Icon_A.GetComponent<Aux_Icon>().Local_Icon.color = Arrow_C;
	  }
	  
	  if(Icon_Dir == 1){
	   Aux_Icon_A = (GameObject) Instantiate(Arrow_Icon, transform.position, transform.rotation);
	   
	   Aux_Icon_A.GetComponent<Aux_Icon>().Local_Icon.sprite = Sprite_Me;
	   
	   Aux_Icon_A.GetComponent<Aux_Icon>().Local_Icon.color = Icon_C;
	   
	   if(SizeIconAdd != 0 && Icon_Dir == 1){
		AuxSizeI = Aux_Icon_A.GetComponent<Aux_Icon>().Local_Icon.gameObject;
		
	    AuxSizeI.GetComponent<RectTransform>().localScale = new Vector3((1f + SizeIconAdd), (1f + SizeIconAdd), 1f);  
	   }
	  }
	  
	  Icon_A = Aux_Icon_A.GetComponent<RectTransform>();
	  
	  Icon_A.SetParent(Ref_Me.transform);
	  
	  Icon_A.position = Ref_Me.transform.position;
	  
	  Icon_A.gameObject.SetActive(true);
	  
	  Icon_A.anchoredPosition = new Vector2(0f, 0f);
	  
	  Icon_A.localScale = new Vector3(1f, 1f, 1f);
	 }
	 
	 if(Icon_M == null){
	  Aux_IconM = (GameObject) Instantiate(Icon, transform.position, transform.rotation);
	  
	  Icon_M = Aux_IconM.GetComponent<RectTransform>();
	  
	  Icon_M.SetParent(Ref_Me.transform);
	  
	  Icon_M.gameObject.SetActive(false);
	  
	  Icon_M.anchoredPosition = new Vector2(0f, 0f);
	  
	  Icon_M.localScale = new Vector3((MP_Ok.IconScale + SizeIconAdd), (MP_Ok.IconScale + SizeIconAdd), 1f);
	  
	  AuxIm = Aux_IconM.GetComponent<Aux_Icon>().Local_Icon;
	  
	  AuxIm.sprite = Sprite_Me;
	  
	  if(Icon_C != new Color32(0,0,0,0)){
	   AuxIm.color = Icon_C;
	  }
	 
	  if(HiddenIcon == 0){
	   AuxIm.color = new Color(AuxIm.color.r, AuxIm.color.g, AuxIm.color.b, 255f);
	   
	   Destroy(Aux_IconM.GetComponent<Rigidbody2D>());
	   Destroy(Aux_IconM.GetComponent<BoxCollider2D>());
	  }else{
	   AuxIm.color = new Color(AuxIm.color.r, AuxIm.color.g, AuxIm.color.b, 0f);
	   
	   Aux_IconM.AddComponent<AuxRadarColy>().AuxIm = AuxIm;
	   Aux_IconM.GetComponent<AuxRadarColy>().TimeAddT = MP_Ok.TimeViewOff;
	   
	   Aux_IconM.GetComponent<AuxRadarColy>().RadarPersecutor = MP_Ok.RadarPersecutor;
	   
	   if(MP_Ok.RadarPersecutor == 0){
	    AuxIm.gameObject.GetComponent<RectTransform>().SetParent(Ref_Me.transform);
	   }
	  }
	 }
	 
	 if(Distance_Arrow > MP_Ok.Min_Dis_Icon){
	  if(Icon_M.gameObject.activeSelf == true){
	   if(Icon_A != null){
	    Icon_A.gameObject.SetActive(true);
	   }
	   
	   Icon_M.gameObject.SetActive(false);
	  }
	  
	  if(Icon_A != null){
	   flip = TargetPos.z > 0 ? 0f : 180f;

	   Icon_A.transform.localEulerAngles = new Vector3(0f, 0f, angle + flip);
	  }
	 }
	 
	 if(Distance_Arrow <= MP_Ok.Min_Dis_Icon){

	  if(Icon_M.gameObject.activeSelf == false){
	   if(Icon_A != null){
	    Icon_A.gameObject.SetActive(false);
	   }
	   Icon_M.gameObject.SetActive(true);
	  }
	   
	  Icon_M.localPosition = new Vector3(TargetPos.x, TargetPos.y, 0f);
	  
	  if(RotIconMe == 1){
	   if(MP_Ok.CamFixed == 0){
	    Icon_M.eulerAngles = new Vector3(0f, 0f, (-transform.eulerAngles.y + MP_Ok.Player_RY.eulerAngles.y));
	   }else{
		Icon_M.eulerAngles = new Vector3(0f, 0f, (-transform.eulerAngles.y + MP_Ok.Cam_MapT.eulerAngles.y));   
	   }
	  }
	 }
	}
}

}
