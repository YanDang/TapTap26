using UnityEngine;

/// <summary>The very same XY boundary is sampled for telegraph vertices and damage containment.</summary>
public struct BossAttackShape
{
    public enum Kind { Circle, Cone, Line }
    public Kind kind;
    public Vector2 origin,direction;
    public float radius,halfAngle,length,width;
    public bool Contains(Vector2 p) {
        Vector2 d=p-origin;
        if(kind==Kind.Line) return BossBrain.InLine(p,origin,direction,length,width);
        return d.magnitude<=radius && (kind==Kind.Circle || Vector2.Angle(direction,d)<=halfAngle);
    }
    public Vector3[] Outline() {
        if(kind==Kind.Line) { Vector2 side=new Vector2(-direction.y,direction.x)*width*.5f; return new Vector3[]{origin-side,origin+side,origin+direction*length+side,origin+direction*length-side,origin-side}; }
        const int count=64; var points=new Vector3[count+3];
        points[0]=kind==Kind.Circle?origin+direction*radius:origin;
        for(int i=0;i<=count;i++) { float a=kind==Kind.Circle?360f*i/count:Mathf.Lerp(-halfAngle,halfAngle,i/(float)count); Vector2 d=Quaternion.Euler(0,0,a)*direction; points[i+1]=origin+d*radius; }
        points[count+2]=points[0]; return points;
    }
}
