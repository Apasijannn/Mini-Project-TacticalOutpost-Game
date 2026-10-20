using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MiniMapModular{
	
public class Custom_MP : MonoBehaviour
{
	[Header("Links")]
    public Image BorderLine_Ima;
	public Image Fund_Ima;
	public Image Central_Ima;
	public Image Coordinates_Ima;
	public Image Ray_Ima;
	public Image Direction_Ima;
	public Image Radar_Ima;
	public Image SpriteTex_Ima;
	
	[Header("Transforms")]
	public RectTransform Map_Status;
	public Transform MPT;
	public Transform MPT1;
	public Transform MPT2;
	public Transform RotMM;
	public Transform DirectionV;
	
	[Header("RectTransforms")]
	public RectTransform RadarT;
	public RectTransform IconPlayerR;
	public RectTransform RadarFixed;
	public RectTransform CoordRadRot;
	
	[Header("GameObjects")]
	public GameObject Type1;
	public GameObject Type2;
	public GameObject Type3;
	public GameObject AngleTextG;
	
	[Header("Textos")]
	public Text AngleRot;
	
	[Header("Auxs_WF")]
	public Canvas Canvas_Control;
	public Camera Cam_Player;
	public Transform WFT;
}

}
