using UnityEngine;
using System.Collections.Generic;
using SVList;
using SVList.Demo;

namespace SVList.Demo
{
    public class DemoInitializer : MonoBehaviour
    {
        [SerializeField] private SVListView _listView;

        void Start()
        {
            var dataList = new List<RankData>();
            for (int i = 0; i < 100000; i++)
            {
                dataList.Add(new RankData
                {
                    PlayerName = $"Player_{i:D6}",
                    Score = Random.Range(0, 999999)
                });
            }

            _listView.Initialize(dataList);
        }
    }
}