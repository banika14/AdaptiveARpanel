using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class PanelSpawner : MonoBehaviour
{
    public ARTrackedImageManager imageManager;
    public AdaptivePanel panelPrefab;

    public enum NoteKind { Static, SittingTimer, WateringSchedule, ReadingGoal }

    [Serializable]
    public class Info
    {
        public string imageName;          
        public string title;
        [TextArea] public string note;    
        public float radius = 0.2f;       

        public NoteKind kind = NoteKind.Static;
        [Tooltip("SittingTimer: break after N minutes. WateringSchedule: water every N days. ReadingGoal: goal in minutes.")]
        public float threshold = 1f;
        [Tooltip("WateringSchedule only: date last watered, as yyyy-MM-dd")]
        public string lastWatered = "2026-11-17";
        [Tooltip("You count as 'using' the object only within this distance (metres).")]
        public float engageDistance = 1.5f;
    }
    public List<Info> infos = new List<Info>();

    public float hideAfterSeconds = 1.0f;  
    [Tooltip("You must face the object within this angle (degrees) to count as using it.")]
    public float engageAngle = 25f;
    [Tooltip("Looking away for less than this (seconds) still counts as using the object.")]
    public float resetAfterSeconds = 5f;
    [Tooltip("SittingTimer: you must be away from the chair this long (seconds) for it to count as a real break.")]
    public float breakSeconds = 30f;
    public float doneMessageSeconds = 2f;   

    readonly Dictionary<string, AdaptivePanel> panels = new Dictionary<string, AdaptivePanel>();
    readonly Dictionary<string, TrackingState> lastState = new Dictionary<string, TrackingState>();
    readonly Dictionary<string, float> lastSeen = new Dictionary<string, float>();      
    readonly Dictionary<string, float> lastEngaged = new Dictionary<string, float>();   
    readonly Dictionary<string, float> sitSeconds = new Dictionary<string, float>();    
    readonly Dictionary<string, float> engagedTime = new Dictionary<string, float>();   
    readonly HashSet<string> breakRequested = new HashSet<string>();                    
    readonly HashSet<string> readingDone = new HashSet<string>();                       
    readonly Dictionary<string, Color> defaultTitleColor = new Dictionary<string, Color>();
    readonly Dictionary<string, string> messageText = new Dictionary<string, string>();  
    readonly Dictionary<string, float> messageUntil = new Dictionary<string, float>();   
    Camera cam;

    void Start()
    {
        cam = Camera.main;
        if (imageManager == null) imageManager = GetComponent<ARTrackedImageManager>();
        if (imageManager == null)
        {
            Debug.LogError("PanelSpawner: no ARTrackedImageManager found. Put this script on XR Origin.");
            enabled = false;
            return;
        }
        int count = imageManager.referenceLibrary != null ? imageManager.referenceLibrary.count : 0;
        Debug.Log($"PanelSpawner: running. Library has {count} images, {infos.Count} Infos entries.");
    }

    void Update()
    {
        
        foreach (var img in imageManager.trackables)
            Attach(img);

        
        if (Input.GetMouseButtonDown(0)) HandleClick();
    }

    void Attach(ARTrackedImage img)
    {
        string key = img.referenceImage.name;

        
        string logKey = string.IsNullOrEmpty(key) ? "(unnamed " + img.trackableId + ")" : key;
        if (!lastState.TryGetValue(logKey, out var prev) || prev != img.trackingState)
        {
            lastState[logKey] = img.trackingState;
            Debug.Log($"PanelSpawner: image '{logKey}' state={img.trackingState} at {img.transform.position}");
        }

        if (string.IsNullOrEmpty(key)) return;   

        var info = infos.Find(i => i.imageName == key);

        if (!panels.ContainsKey(key))
        {
            var p = Instantiate(panelPrefab);
            p.name = "InfoPanel_" + key;
            p.nameText.text = info != null ? info.title : key;
            p.targetRadius = info != null ? info.radius : 0.2f;
            p.target = img.transform;
            defaultTitleColor[key] = p.nameText.color;   
            panels[key] = p;
            Debug.Log($"PanelSpawner: created panel for '{key}'");
        }

        bool tracking = img.trackingState == TrackingState.Tracking;
        if (tracking) lastSeen[key] = Time.time;

        
        bool engaged = tracking && info != null && IsEngaged(img.transform.position, info.engageDistance);

        if (info != null && info.kind == NoteKind.SittingTimer) UpdateChair(key, engaged);
        if (info != null && info.kind == NoteKind.ReadingGoal) UpdateBook(key, info, engaged);
        if (engaged) lastEngaged[key] = Time.time;

        
        bool visible = lastSeen.TryGetValue(key, out var seenAt) && Time.time - seenAt < hideAfterSeconds;
        var panel = panels[key];
        panel.gameObject.SetActive(visible);

        
        if (visible)
        {
            string text = BuildNote(key, info);
            if (panel.noteText.text != text) panel.noteText.text = text;

            float u = Urgency(key, info);
            panel.nameText.color = u < 0f ? defaultTitleColor[key] : UrgencyColor(u);
        }
    }

    
    void UpdateChair(string key, bool engaged)
    {
        float away = AwayFor(key);   // seconds since you last used the chair

        if (engaged)
        {
            
            if (breakRequested.Contains(key) && away > resetAfterSeconds && away < breakSeconds)
            {
                breakRequested.Remove(key);
                ShowMessage(key, "Break too short, timer continues");
            }
            
            if (!breakRequested.Contains(key))
                sitSeconds[key] = Sit(key) + Time.deltaTime;
        }
        else if (away >= breakSeconds && (Sit(key) > 0f || breakRequested.Contains(key)))
        {
            
            sitSeconds[key] = 0f;
            breakRequested.Remove(key);
            ShowMessage(key, "Break complete, timer reset");
        }
    }

    
    void UpdateBook(string key, Info info, bool engaged)
    {
        if (!engaged || readingDone.Contains(key)) return;   

        engagedTime[key] = Read(key) + Time.deltaTime;
        if (Read(key) >= info.threshold * 60f)
        {
            readingDone.Add(key);                              
            ShowMessage(key, "Reading goal reached!");
        }
    }

    
    bool IsEngaged(Vector3 point, float maxDistance)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;
        Vector3 toObject = point - cam.transform.position;
        if (toObject.magnitude > maxDistance) return false;
        return Vector3.Angle(cam.transform.forward, toObject) <= engageAngle;
    }

    
    string BuildNote(string key, Info info)
    {
        if (info == null) return "";
        if (HasMessage(key)) return messageText[key];   

        switch (info.kind)
        {
            case NoteKind.SittingTimer:
                {
                    float sit = Sit(key);
                    float away = AwayFor(key);
                    if (breakRequested.Contains(key))
                    {
                        return away <= resetAfterSeconds
                            ? "Break started: step away from the chair"
                            : $"On break {FormatTime(away)} / {FormatTime(breakSeconds)}";
                    }
                    if (sit <= 0f) return "Not in use";
                    if (away > resetAfterSeconds)
                        return $"Paused at {FormatTime(sit)} (away {FormatTime(away)})";
                    return sit >= info.threshold * 60f
                        ? $"Sitting {FormatTime(sit)}, time for a break! (click)"
                        : $"Sitting {FormatTime(sit)}";
                }

            case NoteKind.WateringSchedule:
                {
                    if (!DateTime.TryParse(info.lastWatered, out var watered))
                        return "Set 'Last Watered' as yyyy-MM-dd";
                    int days = (DateTime.Today - watered.Date).Days;
                    int next = Mathf.RoundToInt(info.threshold) - days;
                    string ago = days == 0 ? "today" : days == 1 ? "1 day ago" : $"{days} days ago";
                    return next <= 0
                        ? $"Water today! (last watered {ago})"
                        : $"Watered {ago}, next in {next} day{(next == 1 ? "" : "s")}";
                }

            case NoteKind.ReadingGoal:
                {
                    float read = Read(key);
                    if (readingDone.Contains(key))
                        return $"Finished: read {FormatTime(read)}. Click to start again";
                    float leftMin = info.threshold - read / 60f;
                    return $"Read {FormatTime(read)}, {leftMin:0.0} min left";
                }

            default:
                return info.note;   
        }
    }

    
    float Urgency(string key, Info info)
    {
        if (info == null) return -1f;
        if (HasMessage(key)) return 0f;   

        switch (info.kind)
        {
            case NoteKind.SittingTimer:
                if (breakRequested.Contains(key)) return 0.25f;   
                return Mathf.Clamp01(Sit(key) / (info.threshold * 60f));

            case NoteKind.WateringSchedule:
                {
                    if (!DateTime.TryParse(info.lastWatered, out var watered)) return -1f;
                    int days = (DateTime.Today - watered.Date).Days;
                    return Mathf.Clamp01(days / Mathf.Max(1f, info.threshold));
                }

            case NoteKind.ReadingGoal:
                if (readingDone.Contains(key)) return 0f;          
                return 0.5f * (1f - Mathf.Clamp01(Read(key) / (info.threshold * 60f)));   

            default:
                return -1f;
        }
    }

    
    static Color UrgencyColor(float u)
    {
        Color green = new Color(0.3f, 1f, 0.3f), yellow = new Color(1f, 0.9f, 0.2f), red = new Color(1f, 0.3f, 0.3f);
        return u <= 0.5f ? Color.Lerp(green, yellow, u * 2f) : Color.Lerp(yellow, red, (u - 0.5f) * 2f);
    }

    void HandleClick()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 10f, LayerMask.GetMask("UI"))) return;  // only panels count

        var clicked = hit.collider.GetComponentInParent<AdaptivePanel>();
        if (clicked == null) return;
        foreach (var pair in panels)
        {
            if (pair.Value == clicked) { Complete(pair.Key); return; }
        }
    }

    void Complete(string key)
    {
        var info = infos.Find(i => i.imageName == key);
        if (info == null) return;

        switch (info.kind)
        {
            case NoteKind.SittingTimer:
                
                if (Sit(key) > 0f && !breakRequested.Contains(key))
                {
                    breakRequested.Add(key);
                    ShowMessage(key, "Break started: step away from the chair");
                }
                break;

            case NoteKind.WateringSchedule:
                info.lastWatered = DateTime.Today.ToString("yyyy-MM-dd");  
                ShowMessage(key, "Done! Watered today");
                break;

            case NoteKind.ReadingGoal:
                if (!readingDone.Contains(key))
                {
                    readingDone.Add(key);           
                    ShowMessage(key, "Reading finished, timer stopped");
                }
                else
                {
                    readingDone.Remove(key);        
                    engagedTime[key] = 0f;
                    ShowMessage(key, "New reading session started");
                }
                break;

            default:
                ShowMessage(key, "Done!");
                break;
        }
        Debug.Log($"PanelSpawner: clicked '{key}'");
    }

    
    void ShowMessage(string key, string text)
    {
        messageText[key] = text;
        messageUntil[key] = Time.time + doneMessageSeconds;
    }

    bool HasMessage(string key) => messageUntil.TryGetValue(key, out var until) && Time.time < until;

    float Sit(string key) => sitSeconds.TryGetValue(key, out var s) ? s : 0f;

    float Read(string key) => engagedTime.TryGetValue(key, out var r) ? r : 0f;

    float AwayFor(string key) => lastEngaged.TryGetValue(key, out var t) ? Time.time - t : float.MaxValue;


    static string FormatTime(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        return $"{s / 60}:{s % 60:00}";
    }
}