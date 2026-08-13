using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core;
using Synthesis.Core.Map;

namespace Synthesis.Data
{
    // 저장된 루프 맵(authored). 경로(코너 포함 셀), 스폰 지점, 석상만 담고 나머지는 로드 시 Core 가 재계산한다.
    // 런타임은 이 SO 를 그대로 LoopMap 으로 복원해 시뮬과 렌더가 같은 맵을 쓴다(시드 생성/베이크 불일치 원천 제거).
    // GridPos 직렬화 의존을 피하려고 좌표는 int 배열로 나눠 저장한다.
    [CreateAssetMenu(menuName = "Synthesis/Map", fileName = "Map")]
    public class MapSO : ScriptableObject
    {
        public int gridWidth;
        public int gridHeight;

        [Tooltip("경로 셀 좌표(코너 포함), 순회 순서. waypointX[i],waypointY[i] 가 한 셀")]
        public int[] waypointX;
        public int[] waypointY;

        [Tooltip("스폰 지점(웨이포인트 인덱스)")]
        public int[] spawnIndices;

        [Tooltip("석상 셀 좌표")]
        public int[] statueX;
        public int[] statueY;

        [Tooltip("석상 체력(Fixed raw)")]
        public long statueHpRaw;

        public int coverageRadius = 4;

        // SO -> Core LoopMap 복원.
        public LoopMap ToLoopMap()
        {
            List<GridPos> cells = new List<GridPos>();
            int n = waypointX != null ? waypointX.Length : 0;
            for (int i = 0; i < n; ++i) cells.Add(new GridPos(waypointX[i], waypointY[i]));

            List<int> spawns = new List<int>();
            if (spawnIndices != null)
            {
                foreach (int s in spawnIndices) spawns.Add(s);
            }

            List<GridPos> statues = new List<GridPos>();
            int sn = statueX != null ? statueX.Length : 0;
            for (int i = 0; i < sn; ++i) statues.Add(new GridPos(statueX[i], statueY[i]));

            return LoopMapGenerator.FromCells(gridWidth, gridHeight, cells, spawns, statues,
                Fixed.FromRaw(statueHpRaw), coverageRadius);
        }

        // Core LoopMap -> SO (에디터에서 현재 맵을 저장할 때).
        public void FromLoopMap(LoopMap map)
        {
            gridWidth = map.gridWidth;
            gridHeight = map.gridHeight;

            int n = map.loopWaypointList.Count;
            waypointX = new int[n];
            waypointY = new int[n];
            for (int i = 0; i < n; ++i)
            {
                waypointX[i] = map.loopWaypointList[i].x;
                waypointY[i] = map.loopWaypointList[i].y;
            }

            spawnIndices = map.spawnIndexList.ToArray();

            int sn = map.statueList.Count;
            statueX = new int[sn];
            statueY = new int[sn];
            for (int i = 0; i < sn; ++i)
            {
                statueX[i] = map.statueList[i].x;
                statueY[i] = map.statueList[i].y;
            }

            statueHpRaw = map.statueHp.raw;
        }
    }
}
