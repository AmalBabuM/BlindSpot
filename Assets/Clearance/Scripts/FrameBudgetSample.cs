using System;
using UnityEngine;

namespace Clearance
{
    // Fixed storage; one timing report after warm-up. Measures CPU/browser pacing, not GPU cost.
    public sealed class FrameBudgetSample : MonoBehaviour
    {
        readonly float[] frames = new float[600];
        int count;
        float warmup = 6;
        public string Result { get; private set; }
        void Update()
        {
            if (Result != null || Time.timeScale == 0 || !Application.isFocused) return;
            if (warmup > 0) { warmup -= Time.unscaledDeltaTime; return; }
            frames[count++] = Time.unscaledDeltaTime * 1000;
            if (count < frames.Length) return;
            float total = 0; int stalls = 0;
            foreach (float frame in frames) { total += frame; if (frame > 50) stalls++; }
            Array.Sort(frames);
            Result = "FRAME SAMPLE: 600 active frames; mean=" + (total / count).ToString("F2") +
                " ms; p95=" + frames[(int)(count * .95f) - 1].ToString("F2") + " ms; >50ms=" + stalls +
                "; canvas=" + Screen.width + "x" + Screen.height + ". This is one device/run, not a no-lag guarantee.";
            Debug.Log(Result);
#if UNITY_EDITOR
            System.IO.File.WriteAllText("Logs/Frame-budget-sample.txt", Result);
#endif
            enabled = false;
        }
    }
}
