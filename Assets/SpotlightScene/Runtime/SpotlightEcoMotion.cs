using UnityEngine;
namespace SpotlightScene {
public sealed class SpotlightEcoMotion : MonoBehaviour {
 public Vector3 origin;
 public float amplitude=.12f, speed=1f, phase;
 void Awake() { origin=transform.localPosition; }
 void Update() { transform.localPosition=origin+Vector3.up*(Mathf.Sin(Time.time*speed+phase)*amplitude); }
}
}
