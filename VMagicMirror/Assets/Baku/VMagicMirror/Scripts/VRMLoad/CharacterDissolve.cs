using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Baku.VMagicMirror
{
    /// <summary> キャラクターをどうにかしてディゾルブさせるクラス </summary>
    public class CharacterDissolve : MonoBehaviour
    {
        [SerializeField] private Transform slidePlane = null;
        [SerializeField] private float appearDuration = 0.5f;

        private float _goalZ = 0;
        
        public void OnVrmLoaded(VrmLoadedInfo info)
        {
            //やりたい事: ロードした瞬間にステルス平面でキャラを隠し、その後に板をスライドさせてキャラを登場させる
            var renderers = info.vrmRoot.GetComponentsInChildren<SkinnedMeshRenderer>();
            slidePlane.gameObject.SetActive(true);
            MoveSlidePlane(renderers);
            StartCoroutine(SlidePlane(
                slidePlane.transform.localPosition.z, _goalZ, appearDuration
            ));
        }

        public void OnVrmDisposing()
        {
            //何もしないが、イベントハンドラが対じゃないと居心地悪いので…
        }
        
        //板ポリをキャラクターの前面に出してキャラを覆い隠す
        private void MoveSlidePlane(IEnumerable<Renderer> renderers)
        {
            var (min, max) = CalculateBoundingBox(renderers);
            slidePlane.transform.position = new Vector3(
                (min.x + max.x) * 0.5f,
                (min.y + max.y) * 0.5f,
                max.z
            );

            float preferredScale = Mathf.Max(max.x - min.x, max.y - min.y);
            slidePlane.transform.localScale = preferredScale * Vector3.one;
            _goalZ = min.z;
        }
        
        //キャラを完全に覆うバウンディングボックスを取得する
        private static (Vector3, Vector3) CalculateBoundingBox(IEnumerable<Renderer> renderers)
        {
            bool isFirst = true;
            var minVec = Vector3.one * 100;
            var maxVec = Vector3.one * (-100);

            foreach (var r in renderers)
            {
                var b = r.bounds;
                if (isFirst)
                {
                    minVec = b.min;
                    maxVec = b.max;
                    isFirst = false;
                }
                else
                {
                    minVec = Vector3.Min(minVec, b.min);
                    maxVec = Vector3.Max(maxVec, b.max);
                }
            }
            
            return (minVec, maxVec);
        }

        private IEnumerator SlidePlane(float startZ, float goalZ, float duration)
        {
            float start = Time.time;
            while (Time.time - start < duration)
            {
                float rate = Mathf.SmoothStep(0, 1, 
                    Mathf.Clamp01((Time.time - start) / duration)
                );

                slidePlane.transform.localPosition = new Vector3(
                    slidePlane.transform.localPosition.x,
                    slidePlane.transform.localPosition.y,
                    Mathf.Lerp(startZ, goalZ, rate)
                    );
                
                yield return null;
            }
            slidePlane.gameObject.SetActive(false);
        }
    }
}
