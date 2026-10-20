using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways, RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
public sealed class OutpostDustBeam : MonoBehaviour
{
    public Light source;
    [Min(.1f)] public float length=11;
    [Range(0,1)] public float density=.075f;
    MeshRenderer beam;
    MaterialPropertyBlock properties;
    void OnEnable()=>Refresh();
    void LateUpdate()=>Refresh();
    public void Refresh()
    {
        if(!beam)beam=GetComponent<MeshRenderer>();
        beam.enabled=source&&source.enabled&&source.gameObject.activeInHierarchy&&source.type==LightType.Spot;
        if(!beam.enabled)return;
        var distance=Mathf.Min(length,source.range);
        var radius=distance*Mathf.Tan(Mathf.Min(source.spotAngle,80)*.5f*Mathf.Deg2Rad);
        transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
        var parent=transform.parent?transform.parent.lossyScale:Vector3.one;
        transform.localScale=new Vector3(radius/Mathf.Max(Mathf.Abs(parent.x),.001f),radius/Mathf.Max(Mathf.Abs(parent.y),.001f),distance/Mathf.Max(Mathf.Abs(parent.z),.001f));
        if(properties==null)properties=new MaterialPropertyBlock();
        properties.SetColor("_BeamTint",source.color*Mathf.Sqrt(Mathf.Max(source.intensity,0)/600));
        properties.SetFloat("_Density",density);
        beam.SetPropertyBlock(properties);
        beam.shadowCastingMode=ShadowCastingMode.Off;beam.receiveShadows=false;
    }
}

