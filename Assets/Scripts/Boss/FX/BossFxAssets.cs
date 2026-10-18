using UnityEngine;
public static class BossVisualRandom {
 static readonly System.Random r=new System.Random(181026);
 public static float value=>(float)r.NextDouble();
 public static float Range(float a,float b)=>Mathf.Lerp(a,b,value);
 public static int Range(int a,int b)=>r.Next(a,b);
}
public static class BossFxAssets {
 static Material material;static Sprite dot;
 public static Shader Shader=>Resources.Load<Shader>("Boss/BossPresentation");
 public static Material Material {get {if(!material)material=new Material(Shader);return material;}}
 public static Sprite Dot {get {if(!dot){var t=new Texture2D(32,32,TextureFormat.RGBA32,false);for(int y=0;y<32;y++)for(int x=0;x<32;x++){float a=Mathf.Clamp01(1-Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/15.5f);t.SetPixel(x,y,new Color(1,1,1,a*a));}t.Apply();dot=Sprite.Create(t,new Rect(0,0,32,32),Vector2.one*.5f,32);}return dot;}}
}
