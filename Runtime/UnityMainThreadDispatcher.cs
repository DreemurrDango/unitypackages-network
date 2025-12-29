using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DreemurrStudio.Network
{
    /// <summary>
    /// 一个线程安全的类，用于持有一个操作队列，并在下一个Update()帧上执行它们。
    /// 它可以用于在其他线程中调用主线程才能执行的操作，例如UI操作。
    /// 这是一个单例，会在首次被访问时自动创建。
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        /// <summary>
        /// 用于存储待执行操作的队列
        /// </summary>
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();
        /// <summary>
        /// 单例实例
        /// </summary>
        private static UnityMainThreadDispatcher _instance = null;

        /// <summary>
        /// 获取UnityMainThreadDispatcher的一个实例。
        /// </summary>
        /// <returns>单例实例。</returns>
        public static UnityMainThreadDispatcher Instance()
        {
            if (_instance == null)
            {
                // 在场景中寻找一个已存在的实例
                _instance = FindObjectOfType<UnityMainThreadDispatcher>();

                if (_instance == null)
                {
                    // 如果没有实例存在，则创建一个新的GameObject并添加此分发器组件
                    GameObject dispatcherObject = new GameObject("UnityMainThreadDispatcher");
                    _instance = dispatcherObject.AddComponent<UnityMainThreadDispatcher>();
                }
            }
            return _instance;
        }

        private void Awake()
        {
            // 确保只有一个实例，并让它在场景切换时不被销毁
            if (_instance != null && _instance != this) Destroy(gameObject);
            else
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }

        /// <summary>
        /// 锁定队列并将一个IEnumerator协程添加到队列中。
        /// </summary>
        /// <param name="action">将从主线程执行的IEnumerator协程函数。</param>
        public void Enqueue(IEnumerator action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(() => {
                    StartCoroutine(action);
                });
            }
        }

        /// <summary>
        /// 锁定队列并将一个Action委托添加到队列中。
        /// </summary>
        /// <param name="action">将从主线程执行的Action委托。</param>
        public void Enqueue(Action action)
        {
            Enqueue(ActionWrapper(action));

            // 这里使用本地函数将Action包装成IEnumerator
            IEnumerator ActionWrapper(Action action)
            {
                action();
                yield return null;
            }
        }

        private void Update()
        {
            // 在主线程的每一帧更新时，检查队列中是否有待执行的操作
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    // 从队列中取出操作并执行
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }
    }
}
