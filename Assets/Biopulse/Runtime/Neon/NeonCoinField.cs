using System.Collections.Generic;
using UnityEngine;

namespace Biopulse.Neon
{
    [System.Serializable]
    public sealed class NeonCoinRoute
    {
        // Local positions on a validated running, jumping or swinging route, in travel order.
        public Vector2[] Points;
        public string Kind;
    }

    public sealed class NeonCoinField : MonoBehaviour
    {
        public GameObject CoinPrefab;
        public NeonCoinRoute[] Routes = new NeonCoinRoute[0];
        private readonly List<NeonCoin> pool = new List<NeonCoin>();
        private int used;
        public int PooledCoinCount => pool.Count;

        public void BeginPopulation()
        {
            used = 0;
            foreach (var coin in pool) coin.gameObject.SetActive(false);
        }

        public void Spawn(Vector2 localPoint)
        {
            if (CoinPrefab == null) return;
            NeonCoin coin;
            if (used < pool.Count) coin = pool[used];
            else
            {
                var go = Instantiate(CoinPrefab, transform);
                go.SetActive(false);
                coin = go.GetComponent<NeonCoin>() ?? go.AddComponent<NeonCoin>();
                pool.Add(coin);
            }
            used++;
            coin.transform.localPosition = localPoint;
            coin.gameObject.SetActive(true);
        }
    }
}
