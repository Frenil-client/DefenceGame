using UnityEngine;

namespace Synthesis.Presentation
{
    // STEP 2/3(재작업). 뷰 - 월드 UI(HP바 등)를 카메라를 향하게 한다. 고정 쿼터뷰라 회전만 맞추면 된다.
    public sealed class Billboard : MonoBehaviour
    {
        private Camera cam;

        private void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            transform.rotation = cam.transform.rotation;
        }
    }
}
