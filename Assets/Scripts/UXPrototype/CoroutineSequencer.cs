using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoroutineSequencer : MonoBehaviour
{
    private Queue<IEnumerator> coroutineQueue = new Queue<IEnumerator>();
    private bool isProcessing = false;

    /// <summary>
    /// 添加多个协程到队列并按顺序执行
    /// </summary>
    public void Enqueue(params IEnumerator[] coroutines)
    {
        foreach (var coroutine in coroutines)
        {
            coroutineQueue.Enqueue(coroutine);
        }
        
        // 如果当前没有在处理，开始处理
        if (!isProcessing)
        {
            StartCoroutine(ProcessQueue());
        }
    }

    /// <summary>
    /// 添加单个协程
    /// </summary>
    public void Enqueue(IEnumerator coroutine)
    {
        coroutineQueue.Enqueue(coroutine);
        
        if (!isProcessing)
        {
            StartCoroutine(ProcessQueue());
        }
    }

    private IEnumerator ProcessQueue()
    {
        isProcessing = true;

        while (coroutineQueue.Count > 0)
        {
            // 取出并执行下一个协程，等待其完成
            yield return StartCoroutine(coroutineQueue.Dequeue());
        }

        isProcessing = false;
    }

    // 清空队列
    public void Clear()
    {
        coroutineQueue.Clear();
        isProcessing = false;
        StopAllCoroutines();
    }
}
