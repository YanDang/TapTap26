using UnityEngine;
namespace SpotlightScene {
public sealed class SpotlightAnchor : MonoBehaviour {
 public string role;
 public Vector3 size = Vector3.one;
 void OnDrawGizmosSelected() { Gizmos.color=Color.yellow; Gizmos.DrawWireCube(transform.position+Vector3.up*.2f,size); }
}
}
