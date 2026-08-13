using System;
using System.Collections.Generic;
using UnityEngine;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 실무 - 오브젝트 풀. 몬스터 60기와 유닛이 매 웨이브 생성/소멸하므로
    // Instantiate/Destroy 대신 재사용한다(GC 스파이크와 생성 히치 방지, MAP_SPEC 10).
    public sealed class GameObjectPool
    {
        private readonly Func<GameObject> factory;
        private readonly Transform poolRoot;
        private readonly Stack<GameObject> idle = new Stack<GameObject>();

        public GameObjectPool(Func<GameObject> factory, Transform poolRoot, int prewarm)
        {
            this.factory = factory;
            this.poolRoot = poolRoot;
            for (int i = 0; i < prewarm; ++i)
            {
                GameObject go = factory();
                Release(go);
            }
        }

        public GameObject Get(Transform parent)
        {
            GameObject go = idle.Count > 0 ? idle.Pop() : factory();
            go.transform.SetParent(parent, false);
            go.SetActive(true);
            IPoolable p = go.GetComponent<IPoolable>();
            if (p != null) p.OnSpawn();
            return go;
        }

        public void Release(GameObject go)
        {
            if (go == null) return;
            IPoolable p = go.GetComponent<IPoolable>();
            if (p != null) p.OnDespawn();
            go.SetActive(false);
            go.transform.SetParent(poolRoot, false);
            idle.Push(go);
        }
    }
}
