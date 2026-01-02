using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class StickySurface : MonoBehaviour
{
    // 記錄原本的爸爸是誰，以便離開時歸還
    private Dictionary<Transform, Transform> originalParents = new Dictionary<Transform, Transform>();

    // 當物體放在抽屜裡時
    private void OnCollisionEnter(Collision collision)
    {
        // 1. 確保撞到的是有 Rigidbody 的物品 (例如鑰匙)
        if (collision.rigidbody != null && !collision.rigidbody.isKinematic)
        {
            Transform target = collision.transform;

            // 2. 如果還沒記錄過它的爸爸，記錄下來
            if (!originalParents.ContainsKey(target))
            {
                originalParents.Add(target, target.parent);
            }

            // 3. 🔥 關鍵：讓鑰匙變成抽屜的子物件
            // 這樣抽屜滑動時，鑰匙會 100% 跟著位移，絕對不會掉
            target.SetParent(this.transform);
        }
    }

    // 當物體被拿起來，或因碰撞離開抽屜表面時
    private void OnCollisionExit(Collision collision)
    {
        Transform target = collision.transform;

        if (originalParents.ContainsKey(target))
        {
            // 4. 🔥 關鍵：還原爸爸 (通常是變回 null 或 World)
            // 只有當它的爸爸還是我的時候才還原 (避免被其他系統搶走後又被我重置)
            if (target.parent == this.transform)
            {
                target.SetParent(originalParents[target]);
            }

            originalParents.Remove(target);
        }
    }
}