using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using mixpanel.queue;
using UnityEngine;
using UnityEngine.Networking;

namespace mixpanel
{
    internal class MixpanelManager : MonoBehaviour
    {
        private const int BatchSize = 50;
        private const int RetryMaxTries = 10;
        private const int PoolFillFrames = 50;
        private const int PoolFillEachFrame = 20;
        
        private List<Value> TrackQueue = new List<Value>(500);
        private List<Value> EngageQueue = new List<Value>(500);
        
        #region Singleton
        
        private static MixpanelManager _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            _instance = new GameObject("Mixpanel").AddComponent<MixpanelManager>();
            Debug.Log($"[Mixpanel] Track Queue Depth: {Mixpanel.TrackQueue.CurrentCountOfItemsInQueue}");
            Debug.Log($"[Mixpanel] Engage Queue Depth: {Mixpanel.EngageQueue.CurrentCountOfItemsInQueue}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            Mixpanel.CollectAutoProperties();
        }
        
        #endregion

        private IEnumerator Start()
        {
            DontDestroyOnLoad(this);
            StartCoroutine(PopulatePools());
            while (true)
            {
                yield return new WaitForSecondsRealtime(MixpanelSettings.Instance.FlushInterval);
                Mixpanel.Flush();
            }
        }
        
        private static IEnumerator PopulatePools()
        {
            for (int i = 0; i < PoolFillFrames; i++)
            {
                Mixpanel.NullPool.Put(Value.Null);
                for (int j = 0; j < PoolFillEachFrame; j++)
                {
                    Mixpanel.ArrayPool.Put(Value.Array);
                    Mixpanel.ObjectPool.Put(Value.Object);
                }
                yield return null;
            }
        }

        private void LateUpdate()
        {
            LateUpdateTrackQueue();
            LateUpdateEngageQueue();
        }

        private void LateUpdateTrackQueue()
        {
            if (TrackQueue.Count == 0) return;
            using (PersistentQueueSession session = Mixpanel.TrackQueue.OpenSession())
            {
                foreach (Value item in TrackQueue)
                {
                    session.Enqueue(Encoding.UTF8.GetBytes(JsonUtility.ToJson(item)));
                    Mixpanel.Put(item);
                }
                session.Flush();
            }
            TrackQueue.Clear();
        }

        private void LateUpdateEngageQueue()
        {
            if (EngageQueue.Count == 0) return;
            using (PersistentQueueSession session = Mixpanel.EngageQueue.OpenSession())
            {
                foreach (Value item in EngageQueue)
                {
                    session.Enqueue(Encoding.UTF8.GetBytes(JsonUtility.ToJson(item)));
                    Mixpanel.Put(item);
                }
                session.Flush();
            }
            EngageQueue.Clear();
        }

        private void DoFlush(string url, PersistentQueue queue)
        {
            int depth = queue.CurrentCountOfItemsInQueue;
            int count = (depth / BatchSize) + (depth % BatchSize != 0 ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                StartCoroutine(DoRequest(url, queue));
            }
        }

        private IEnumerator DoRequest(string url, PersistentQueue queue, int retryCount = 0)
        {
            int count = 0;
            Value batch = Mixpanel.ArrayPool.Get();
            using (PersistentQueueSession session = queue.OpenSession())
            {
                while (count < BatchSize)
                {
                    byte[] data = session.Dequeue();
                    if(data == null)
                        break;
                    var jsonString = Encoding.UTF8.GetString(data).Trim();
                    if (string.IsNullOrEmpty(jsonString))
                    {
                        Debug.LogError($"[Mixpanel] empty json in queue");
                        continue;
                    }
                    try
                    {
                        batch.Add(JsonUtility.FromJson<Value>(jsonString));
                        ++count;
                    }
                    catch (ArgumentException exception)
                    {
                        Debug.LogError($"[Mixpanel] Error while deserializing json: {jsonString}\n {exception}");
                    }
                }

                string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(batch.ToString()));
                if (MixpanelSettings.Instance.ShowDebug) Debug.Log($"[Mixpanel] Sending Request - '{url}' with payload '{payload}'");
                WWWForm form = new WWWForm();
                form.AddField("data", payload);
                UnityWebRequest request = UnityWebRequest.Post(url, form);
                yield return request.SendWebRequest();
                while (!request.isDone) yield return new WaitForEndOfFrame();
                if (!request.isNetworkError && !request.isHttpError)
                {
                    session.Flush();
                    Mixpanel.Put(batch);
                    yield break;
                }
                if (retryCount > RetryMaxTries) yield break;
            }
            retryCount += 1;
            // 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024 = 2046 seconds total
            yield return new WaitForSecondsRealtime((float)Math.Pow(2, retryCount));
            StartCoroutine(DoRequest(url, queue, retryCount));
        }

        #region Static

        internal static void EnqueueTrack(Value data)
        {
            _instance.TrackQueue.Add(data);
        }

        internal static void EnqueueEngage(Value data)
        {
            _instance.EngageQueue.Add(data);
        }

        internal static void Flush()
        {
            _instance.DoFlush(MixpanelSettings.Instance.TrackUrl, Mixpanel.TrackQueue);
            _instance.DoFlush(MixpanelSettings.Instance.EngageUrl, Mixpanel.EngageQueue);
        }

        #endregion
    }
}
