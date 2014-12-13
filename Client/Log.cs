using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace DarkMultiPlayer
{
    public class DarkLog
    {
        public static Queue<string> messageQueue = new Queue<string>();
        private static object externalLogLock = new object();

        public static void Debug(string message)
        {
            //Use messageQueue if looking for messages that don't normally show up in the log.

            messageQueue.Enqueue("[" + UnityEngine.Time.realtimeSinceStartup + "] DarkMultiPlayer: " + message);
            //UnityEngine.Debug.Log("[" + UnityEngine.Time.realtimeSinceStartup + "] DarkMultiPlayer: " + message);
        }

        public static void Update()
        {
            while (messageQueue.Count > 0)
            {
                string message = messageQueue.Dequeue();
                UnityEngine.Debug.Log(message);
                /*
                using (StreamWriter sw = new StreamWriter("DarkLog.txt", true, System.Text.Encoding.UTF8)) {
                    sw.WriteLine(message);
                }
                */
            }
        }

        public static void ExternalLog(string debugText)
        {
            lock (externalLogLock)
            {
                using (StreamWriter sw = new StreamWriter(Path.Combine(KSPUtil.ApplicationRootPath, "DMP.log"), true))
                {
                    sw.WriteLine(debugText);
                }
            }
        }

        //TO DEBUG VECTORS
        /*
        public static GameObject line1;
        public static LineRenderer renderer1;
        line1 = new GameObject();
        renderer1 = line1.AddComponent<LineRenderer>();
        renderer1.SetWidth(0.1f, 0.1f);
        renderer1.SetVertexCount(2);
        renderer1.SetColors(Color.red, Color.red);
        Texture2D redTex = new Texture2D(1, 1);
        redTex.SetPixel(0, 0, Color.red);
        redTex.Apply();
        renderer1.material = new Material(Shader.Find("Unlit/Texture"));
        renderer1.material.mainTexture = redTex;
        renderer1.SetPosition(0, src);
        renderer1.SetPosition(1, dst);
        */
    }
}
