using System.Collections;
using UnityEngine;

/// <summary>
/// GameObjectの表示だけを遅延させ、非表示は即座に行うためのコンポーネント。
/// TalkCondition の onCompleted から GameObject.SetActive の代わりに呼び出す想定。
/// 対象オブジェクトが非表示の間はコルーチンを開始できないため、
/// このコンポーネント自体は常にアクティブな別のオブジェクトに置き、
/// target に表示/非表示したいオブジェクトを参照させて使う。
/// </summary>
public class DelayedActivator : MonoBehaviour
{
    [Tooltip("表示/非表示を切り替える対象。未指定ならこのGameObject自身")]
    public GameObject target;

    [Tooltip("表示するまでの待ち時間（秒）")]
    public float delaySeconds = 3f;

    Coroutine pending;

    GameObject Target => target != null ? target : gameObject;

    // 数秒待ってから表示する
    public void ActivateDelayed()
    {
        if (pending != null) StopCoroutine(pending);
        pending = StartCoroutine(ActivateAfterDelay());
    }

    // 即座に非表示にする（保留中の表示予約があればキャンセルする）
    public void DeactivateImmediate()
    {
        if (pending != null)
        {
            StopCoroutine(pending);
            pending = null;
        }
        Target.SetActive(false);
    }

    IEnumerator ActivateAfterDelay()
    {
        yield return new WaitForSeconds(delaySeconds);
        Target.SetActive(true);
        pending = null;
    }
}
