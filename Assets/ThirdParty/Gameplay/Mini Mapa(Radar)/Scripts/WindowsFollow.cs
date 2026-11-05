using UnityEngine;
using UnityEngine.UI;

namespace MiniMapModular{
	
public class WindowsFollow : MonoBehaviour
{
	private Custom_MP MS;
	private Aux_Icon SetMeDate;
	[Space]
	public GameObject Look_S;
	private GameObject AuxLook;
	
	[Header("Canvas")]
	private Canvas Canvas_Control;
	private RectTransform CanvasT;
	private Vector2 Canvas_Posy;
	
	[Header("Cam")]
	public Vector2 ScreenAdjust = new Vector2(50f, 75f);
	private Camera Cam;
	private Vector2 ScreenLimit;
	private Vector3 ScreenPosy;
	private float LimitY;
	private float LimitX;
	
	[Header("Target")]
	private Transform Target;
	private float Distance_Target;
	
	[Header("Text")]
	[Range(0f,1f)] public int DisTextOn = 0;
	private RectTransform Look_Icon;
	private GameObject Loocked_Icon;
	private int AuxDis;
	private Text DisText;
	private GameObject AuxIconText;
	
	[Header("Custom")]
	[Space]
	public Sprite Sprite_Look;
	public Sprite Sprite_Locked;
	public Sprite Sprite_Text;
	private Image ImaLook;
	private Image ImaLocked;
	public Color Icon_C;
	public Color SubIcon_C;
	public Color IconText_C;
	public Color Text_C;
	
	void Start(){
	 Target = transform;
	 
	 MS = GameObject.FindFirstObjectByType<Custom_MP>();
	 
	 Cam = MS.Cam_Player;
	 
	 Canvas_Control = MS.Canvas_Control;
	 
	 CanvasT = Canvas_Control.GetComponent<RectTransform>();
	 
	 AuxLook = (GameObject) Instantiate(Look_S, MS.WFT.position, MS.WFT.rotation);
	 
	 AuxLook.transform.SetParent(MS.WFT);
	 
	 SetMeDate = AuxLook.GetComponent<Aux_Icon>();
	 
	 Look_Icon = AuxLook.GetComponent<RectTransform>();
	 Loocked_Icon = SetMeDate.Local_IconAux.gameObject;
	 AuxIconText = SetMeDate.Local_IconText.gameObject;
	 DisText = SetMeDate.Local_Text;
	 
	 if(Icon_C != new Color32(0,0,0,0) && SetMeDate.Local_Icon.color != Icon_C){
	  SetMeDate.Local_Icon.color = Icon_C;
	 }
	 
	 if(SubIcon_C != new Color32(0,0,0,0) && SetMeDate.Local_IconAux.color != IconText_C){
	  SetMeDate.Local_IconAux.color = IconText_C;
	 }
	  
	 if(IconText_C != new Color32(0,0,0,0) && SetMeDate.Local_IconText.color != IconText_C){
	  SetMeDate.Local_IconText.color = IconText_C;
	 }
	 
	 if(AuxIconText.activeSelf == false && DisTextOn == 1){
	  AuxIconText.SetActive(true);
	 }
	 
	 if(AuxIconText.activeSelf == true){
	  if(DisTextOn == 0){
	   AuxIconText.SetActive(false);
	  }
	  
	  if(Text_C != new Color32(0,0,0,0) && SetMeDate.Local_Text.color != Text_C){
	   SetMeDate.Local_Text.color = Text_C;
	  }
	 }
	 
	 SetMeDate.Local_Icon.sprite = Sprite_Look;
	 SetMeDate.Local_IconAux.sprite = Sprite_Locked;
	 SetMeDate.Local_IconText.sprite = Sprite_Text;
	}
	
	void OnDisable(){
	 Destroy(AuxLook);
	}
	
	void LateUpdate(){
	 if(DisTextOn == 1){
	  Distance_Target = Vector3.Distance(Target.position, Cam.gameObject.transform.position);
	  
	  AuxDis = (int)Distance_Target;
	  
	  DisText.text = AuxDis.ToString() + "m";
	 }
	 
	 ScreenPosy = Cam.WorldToScreenPoint(Target.position);
	 
     if(ScreenPosy.z < 0){
      ScreenPosy *= -1;
     }
	 
	  LimitX = Mathf.Clamp(ScreenPosy.x, ScreenAdjust.x, Screen.width - ScreenAdjust.x);
      LimitY = Mathf.Clamp(ScreenPosy.y, ScreenAdjust.y, Screen.height - ScreenAdjust.y);

	  if(LimitX != ScreenPosy.x && Loocked_Icon.activeSelf == true || 
	     LimitY != ScreenPosy.y && Loocked_Icon.activeSelf == true){
	   Loocked_Icon.SetActive(false);
	  }
	  
	  if(LimitX == ScreenPosy.x && LimitY == ScreenPosy.y && Loocked_Icon.activeSelf == false){
	   Loocked_Icon.SetActive(true);
	  }
	  
      ScreenLimit = new Vector2(LimitX, LimitY);
	  
      RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasT, ScreenLimit,
      Canvas_Control.renderMode == RenderMode.ScreenSpaceOverlay ? null : Cam, out Canvas_Posy);
			
      Look_Icon.anchoredPosition = Canvas_Posy;
	}
	
}

}
