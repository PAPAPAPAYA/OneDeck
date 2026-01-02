using UnityEngine;

// this script is used to create linger effect resolver to resolve linger effects
public class LingerEffectResolverMaker : MonoBehaviour
{
    public GameObject lingerEffectResolverToMake;

    public void MakeLingerEffectResolver()
    {
        var resolver = Instantiate(lingerEffectResolverToMake, LingeringEffectManager.Me.transform);
    }
}
