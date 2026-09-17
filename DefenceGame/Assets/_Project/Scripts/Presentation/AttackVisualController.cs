using System.Collections.Generic;
using UnityEngine;
using Synthesis.Core.Simulation;

namespace Synthesis.Presentation
{
    // STEP 3. 기반 도구 - 한 평타의 표시 결과. 피해 적용 전에 대상 위치를 기록한다.
    public sealed class AttackVisualData
    {
        public readonly List<Vector3> pointList = new List<Vector3>();
        public readonly List<float> radiusList = new List<float>(); // 셀 단위 반경. 셀 크기가 1 이라 월드 단위와 같다
        public Vector3 areaCenter;
        public bool strong;

        public void Clear()
        {
            pointList.Clear();
            radiusList.Clear();
            areaCenter = Vector3.zero;
            strong = false;
        }
    }

    // STEP 3. 핵심 - 공격 표시의 생성, 재사용, 수명을 소유한다. 전투 판정은 하지 않는다.
    // 씬 참조 추가 없이 전투 컴포넌트가 소유하며 파괴 시 Dispose 를 호출한다.
    public sealed class AttackVisualController : System.IDisposable
    {
        private const int RingSegments = 48;
        private const float BeamWidth = 0.07f;
        private const float StrongWidth = 0.16f; // 배수가 걸린 평타. 색과 굵기를 둘 다 바꿔야 셰이더와 무관하게 갈린다
        private const float RingWidth = 0.06f;
        private static readonly Color BeamColor = new Color(1f, 0.92f, 0.35f);
        private static readonly Color StrongColor = new Color(1f, 0.40f, 0.15f);
        private static readonly Color RingColor = new Color(1f, 0.55f, 0.20f);

        // 켜짐 여부를 남은 시간과 따로 둔다. 남은 시간만으로 판정하면 표시 시간을 0 으로 맞췄을 때
        //   처음부터 만료 상태라 끄는 처리가 한 번도 돌지 않고 켜진 채로 남는다.
        private sealed class BeamGroup
        {
            public readonly List<LineRenderer> rendererList = new List<LineRenderer>();
            public float remaining;
            public bool visible;
        }

        private sealed class AreaRing
        {
            public LineRenderer renderer;
            public float remaining;
            public bool visible;
        }

        private readonly Transform parent;
        private readonly float beamSeconds;
        private readonly float ringSeconds;
        private readonly float groundY;
        private readonly Dictionary<LoopUnit, BeamGroup> beamDict = new Dictionary<LoopUnit, BeamGroup>();
        private readonly List<AreaRing> ringList = new List<AreaRing>();
        private Material beamMaterial;
        private Material strongMaterial;
        private Material ringMaterial;

        public AttackVisualController(Transform parent, float beamSeconds, float ringSeconds, float groundY)
        {
            this.parent = parent;
            this.beamSeconds = beamSeconds;
            this.ringSeconds = ringSeconds;
            this.groundY = groundY;
        }

        public void Show(LoopUnit unit, Vector3 origin, AttackVisualData data)
        {
            if (data.pointList.Count == 0) return;
            BeamGroup group;
            if (!beamDict.TryGetValue(unit, out group))
            {
                group = new BeamGroup();
                beamDict.Add(unit, group);
            }

            var color = data.strong ? StrongColor : BeamColor;
            var material = GetBeamMaterial(data.strong);
            for (int i = 0; i < data.pointList.Count; ++i)
            {
                if (i == group.rendererList.Count)
                {
                    group.rendererList.Add(CreateBeamRenderer());
                }
                // 씬 언로드나 도메인 리로드로 오브젝트만 사라질 수 있다. 목록에 빈 칸이 남으면 다시 만든다.
                if (group.rendererList[i] == null) group.rendererList[i] = CreateBeamRenderer();

                var renderer = group.rendererList[i];
                renderer.SetPosition(0, origin);
                renderer.SetPosition(1, data.pointList[i]);
                renderer.widthMultiplier = data.strong ? StrongWidth : BeamWidth;
                renderer.sharedMaterial = material;
                renderer.startColor = color;
                renderer.endColor = new Color(color.r, color.g, color.b, 0.35f);
                renderer.enabled = true;
            }
            for (int i = data.pointList.Count; i < group.rendererList.Count; ++i)
            {
                if (group.rendererList[i] != null) group.rendererList[i].enabled = false;
            }
            group.remaining = beamSeconds;
            group.visible = true;

            foreach (float radius in data.radiusList)
            {
                // 전투 반경은 셀 단위다. 맵 셀 크기가 1 이므로 월드 반경과 같다(LoopMapView.cellSize).
                //   사거리 링(RangeIndicator)도 같은 전제를 쓴다. 둘의 전제가 갈라지면 두 원의 크기가 달라진다.
                ShowAreaRing(data.areaCenter, radius);
            }
        }

        // 꺼진 뒤에는 건드리지 않는다. 남은 시간을 계속 깎으면 만료 후에도 매 프레임 전부를 다시 끄게 된다.
        public void Tick(float elapsed)
        {
            foreach (KeyValuePair<LoopUnit, BeamGroup> pair in beamDict)
            {
                var group = pair.Value;
                if (!group.visible)
                {
                    continue;
                }
                group.remaining -= elapsed;
                if (group.remaining > 0f)
                {
                    continue;
                }
                group.remaining = 0f;
                group.visible = false;
                foreach (LineRenderer renderer in group.rendererList)
                {
                    if (renderer != null) renderer.enabled = false;
                }
            }
            foreach (AreaRing ring in ringList)
            {
                if (!ring.visible)
                {
                    continue;
                }
                ring.remaining -= elapsed;
                if (ring.remaining > 0f)
                {
                    continue;
                }
                ring.remaining = 0f;
                ring.visible = false;
                if (ring.renderer != null) ring.renderer.enabled = false;
            }
        }

        public void RemoveUnit(LoopUnit unit)
        {
            BeamGroup group;
            if (!beamDict.TryGetValue(unit, out group)) return;
            DestroyBeams(group);
            beamDict.Remove(unit);
        }

        public void Reset()
        {
            foreach (KeyValuePair<LoopUnit, BeamGroup> pair in beamDict)
            {
                DestroyBeams(pair.Value);
            }
            beamDict.Clear();
            foreach (AreaRing ring in ringList)
            {
                ring.remaining = 0f;
                ring.visible = false;
                if (ring.renderer != null) ring.renderer.enabled = false;
            }
        }

        public void Dispose()
        {
            Reset();
            foreach (AreaRing ring in ringList)
            {
                if (ring.renderer != null) Object.Destroy(ring.renderer.gameObject);
            }
            ringList.Clear();
            if (beamMaterial != null) Object.Destroy(beamMaterial);
            if (strongMaterial != null) Object.Destroy(strongMaterial);
            if (ringMaterial != null) Object.Destroy(ringMaterial);
        }

        private static void DestroyBeams(BeamGroup group)
        {
            foreach (LineRenderer renderer in group.rendererList)
            {
                if (renderer != null) Object.Destroy(renderer.gameObject);
            }
        }

        private void ShowAreaRing(Vector3 center, float radius)
        {
            if (radius <= 0f) return;
            var ring = GetFreeRing();
            center.y = groundY;
            for (int i = 0; i < RingSegments; ++i)
            {
                var angle = Mathf.PI * 2f * i / RingSegments;
                ring.renderer.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * radius,
                    center.y, center.z + Mathf.Sin(angle) * radius));
            }
            ring.remaining = ringSeconds;
            ring.visible = true;
            ring.renderer.enabled = true;
        }

        private AreaRing GetFreeRing()
        {
            foreach (AreaRing ring in ringList)
            {
                if (ring.visible)
                {
                    continue;
                }
                // 빔과 같은 이유로 오브젝트만 사라졌을 수 있다. 자리는 그대로 두고 렌더러만 되살린다.
                if (ring.renderer == null) ring.renderer = CreateRingRenderer();
                return ring;
            }

            var result = new AreaRing
            {
                renderer = CreateRingRenderer()
            };
            ringList.Add(result);
            return result;
        }

        private LineRenderer CreateBeamRenderer()
        {
            return CreateRenderer("AttackBeam", 2, false);
        }

        private LineRenderer CreateRingRenderer()
        {
            var renderer = CreateRenderer("AreaRing", RingSegments, true);
            renderer.widthMultiplier = RingWidth;
            if (ringMaterial == null) ringMaterial = CreateMaterial(RingColor);
            renderer.sharedMaterial = ringMaterial;
            renderer.startColor = RingColor;
            renderer.endColor = RingColor;
            return renderer;
        }

        private LineRenderer CreateRenderer(string name, int positions, bool loop)
        {
            var instance = new GameObject(name);
            instance.transform.SetParent(parent, false);
            var renderer = instance.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.positionCount = positions;
            renderer.loop = loop;
            renderer.numCapVertices = loop ? 0 : 2;
            renderer.enabled = false;
            return renderer;
        }

        private Material GetBeamMaterial(bool strong)
        {
            if (strong)
            {
                if (strongMaterial == null) strongMaterial = CreateMaterial(StrongColor);
                return strongMaterial;
            }
            if (beamMaterial == null) beamMaterial = CreateMaterial(BeamColor);
            return beamMaterial;
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }
    }
}
