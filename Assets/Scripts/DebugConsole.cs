using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class DebugConsole : MonoBehaviour
{
    private Queue<string> logQueue = new Queue<string>();
    private ConcurrentQueue<string> threadLogQueue = new ConcurrentQueue<string>();
    private const int maxLogCount = 50;
    
    private bool isConsoleVisible = true;
    private Vector2 scrollPosition = Vector2.zero;

    private void OnEnable()
    {
        Application.logMessageReceivedThreaded += HandleLogThreaded;
    }

    private void OnDisable()
    {
        Application.logMessageReceivedThreaded -= HandleLogThreaded;
    }

    private void HandleLogThreaded(string logString, string stackTrace, LogType type)
    {
        threadLogQueue.Enqueue($"[{type}] {logString}");
    }

    private void Update()
    {
        while (threadLogQueue.TryDequeue(out string log))
        {
            logQueue.Enqueue(log);

            if (logQueue.Count > maxLogCount)
            {
                logQueue.Dequeue();
            }
        }
    }

    private void OnGUI()
    {
        if (GUILayout.Button(isConsoleVisible ? "Hide Console" : "Show Console", GUILayout.Width(150)))
        {
            isConsoleVisible = !isConsoleVisible;
        }
        
        if (!isConsoleVisible)
        {
            return;
        }
        
        GUILayout.BeginVertical("box");
        
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300)); // Высота 300px, можно изменить
        foreach (var log in logQueue)
        {
            GUILayout.Label(log);
        }
        GUILayout.EndScrollView();

        if (GUILayout.Button("Clear Logs", GUILayout.Width(150)))
        {
            logQueue.Clear();
        }

        GUILayout.EndVertical();
    }
}