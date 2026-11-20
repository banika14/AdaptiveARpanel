using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class AdaptivePanel : MonoBehaviour
{
    [Header("Target")]
    public Transform target;               
    public float targetRadius = 0.2f;      

    [Header("Content")]
    public TMP_Text nameText;
    public TMP_Text noteText;

    [Header("Behaviour")]
    public float offset = 0.15f;           
    public float moveSpeed = 4f;           
    public float switchThreshold = 0.25f;  
    public float minScreenSeparation = 0.18f; 
    public LayerMask occluders;            
    public float detailDistance = 2.5f;    

    static readonly List<AdaptivePanel> all = new List<AdaptivePanel>();
    Camera cam;
    LineRenderer tether;
    Vector3 currentDir = Vector3.up;

    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    void Start()
    {
        cam = Camera.main;
        tether = GetComponent<LineRenderer>();
        if (target != null) transform.position = target.position + Vector3.up * (targetRadius + offset);
    }

    void LateUpdate()
    {
        if (target == null || cam == null) return;

        
        Vector3 toCam = cam.transform.position - target.position;
        toCam.y = 0f;
        toCam = toCam.sqrMagnitude > 0.0001f ? toCam.normalized : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, toCam).normalized;

        Vector3[] dirs = {
            right, -right, Vector3.up,
            (right + Vector3.up).normalized, (-right + Vector3.up).normalized
        };

        
        float currentScore = Score(currentDir, right);
        float bestScore = float.NegativeInfinity;
        Vector3 bestDir = currentDir;
        foreach (var d in dirs)
        {
            float s = Score(d, right);
            if (s > bestScore) { bestScore = s; bestDir = d; }
        }
        if (bestScore > currentScore + switchThreshold) currentDir = bestDir;

        
        Vector3 desired = target.position + currentDir * (targetRadius + offset);
        transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * moveSpeed);
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

        
        if (tether != null)
        {
            tether.SetPosition(0, target.position);
            tether.SetPosition(1, transform.position);
        }

        
        float dist = Vector3.Distance(cam.transform.position, transform.position);
        transform.localScale = Vector3.one * 0.001f * Mathf.Clamp(dist / 1.5f, 0.7f, 2f);

        
        if (noteText != null) noteText.gameObject.SetActive(dist <= detailDistance);
    }

    float Score(Vector3 dir, Vector3 right)
    {
        Vector3 pos = target.position + dir * (targetRadius + offset);
        float score = 0f;

        
        Vector3 vp = cam.WorldToViewportPoint(pos);
        bool inView = vp.z > 0 && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.05f && vp.y < 0.95f;
        score += inView ? 1f : -2f;

        
        if (Physics.Linecast(cam.transform.position, pos, occluders)) score -= 1.5f;

        
        foreach (var other in all)
        {
            if (other == this) continue;
            Vector3 ovp = cam.WorldToViewportPoint(other.transform.position);
            if (Vector2.Distance(vp, ovp) < minScreenSeparation) score -= 1f;
        }

       
        if (dir == right) score += 0.1f;

        return score;
    }
}