using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MiniMapModular{
	
public class Aux_M_R : MonoBehaviour
{
	[Space]
    public Transform RotX, RotY;
	
	[Space]
	[Range(-100f,100f)] public float Sensitivity = 5f, Speed = 6f;
	private float Vertical, horizontal;
	
	private Vector2 Eixo;
	
	private Vector3 Direction;
	
	private Rigidbody Player_Rigi;
	
	void Start(){
     Player_Rigi  = gameObject.GetComponent<Rigidbody>();
    }
	
	void Update(){
	 //rotation
	 Eixo.y += Input.GetAxis("Mouse X") * Sensitivity;
     Eixo.x -= Input.GetAxis("Mouse Y") * Sensitivity;
	 
	 if(Eixo.x > 15f){
	  Eixo.x = 15f;
	 }
		
     if(Eixo.x < -70f){
	  Eixo.x = -70f;
	 }
	  
	 RotX.localRotation = Quaternion.Euler(Eixo.x, 0f, 0f);
	 RotY.localRotation = Quaternion.Euler(0f, Eixo.y, 0f);
	 
	 //move
	 Vertical = Input.GetAxis("Vertical");
	 horizontal = Input.GetAxis("Horizontal");
	 
	 Player_Rigi.linearVelocity = transform.TransformVector(new Vector3(horizontal*Speed,Player_Rigi.linearVelocity.y,Vertical*Speed));
	}
}

}
