using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MiniMapModular{
	
public class Destroy_Me : MonoBehaviour
{
    
    void Update(){
     if(Input.GetKeyDown("p")){
	  Destroy(gameObject);
	 }
    }
}

}
