using System.Collections;
using System.Linq;
using UnityEngine;

// Dragon Cave v2: zet de draak op de juiste maat en in het midden van de grot,
// NADAT de animatie loopt (het model en de animaties gebruiken verschillende eenheden,
// dus meten in de editor alleen is niet betrouwbaar). Meet op de botten, niet op de mesh.
public class DragonFit : MonoBehaviour
{
    public float targetLength = 6f;          // kop tot staart, horizontaal, in meters
    public Vector3 center = Vector3.zero;    // midden van de grot
    public float floorY = 0f;
    public Transform nest;                   // ei-nest, wordt tussen de voorpoten gezet

    IEnumerator Start()
    {
        yield return null;   // eerst een paar frames animatie laten lopen
        yield return null;
        Fit();
    }

    public void Fit()
    {
        Bounds b = BoneBounds();
        float size = Mathf.Max(b.size.x, b.size.z);
        if (size < 0.0001f) return;

        transform.localScale *= targetLength / size;
        b = BoneBounds();
        float lift = targetLength * 0.02f;   // botten liggen iets boven de onderkant van de huid
        transform.position += new Vector3(center.x - b.center.x, floorY + lift - b.min.y, center.z - b.center.z);

        if (nest != null)
        {
            var hands = GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Hand")).ToList();
            Vector3 p;
            if (hands.Count >= 2) p = (hands[0].position + hands[1].position) * 0.5f + transform.forward * targetLength * 0.08f;
            else p = center + transform.forward * targetLength * 0.55f;
            nest.position = new Vector3(p.x, floorY, p.z);
        }
        Debug.Log("[Dragon Cave v2] Draak in Play: was " + size.ToString("0.00") + ", nu " + targetLength.ToString("0.0") + " m, in het midden gezet.");
    }

    Bounds BoneBounds()
    {
        var bones = GetComponentsInChildren<Transform>().Where(t => t != transform && t.GetComponent<Renderer>() == null).ToArray();
        if (bones.Length == 0) return new Bounds(transform.position, Vector3.zero);
        var b = new Bounds(bones[0].position, Vector3.zero);
        foreach (var t in bones) b.Encapsulate(t.position);
        return b;
    }
}
