﻿using System;
using System.IO;
using Baku.VMagicMirror.IK;
using UnityEngine;
using UniHumanoid;
using VRM;
using Zenject;

namespace Baku.VMagicMirror
{
    using static ExceptionUtils;

    /// <summary>VRMのロード処理をやるやつ</summary>
    public class VRMLoadController : MonoBehaviour, IVRMLoadable
    {
        //[SerializeField] private VrmLoadSetting loadSetting = default;
        [SerializeField] private RuntimeAnimatorController animatorController = null;

        public event Action<VrmLoadedInfo> PreVrmLoaded;
        public event Action<VrmLoadedInfo> VrmLoaded; 
        public event Action VrmDisposing;

        private IKTargetTransforms _ikTargets = null;
        private VRMPreviewCanvas _previewCanvas = null;
        private HumanPoseTransfer _humanPoseTransferTarget = null;

        [Inject]
        public void Initialize(
            IMessageReceiver receiver,
            VRMPreviewCanvas previewCanvas,
            IKTargetTransforms ikTargets
            )
        {
            _previewCanvas = previewCanvas;
            _ikTargets = ikTargets;
            receiver.AssignCommandHandler(
                VmmCommands.OpenVrmPreview,
                message => LoadModelForPreview(message.Content)
                );
            receiver.AssignCommandHandler(
                VmmCommands.OpenVrm,
                message =>
                {
                    previewCanvas.Hide();
                    LoadModel(message.Content);
                });
            receiver.AssignCommandHandler(
                VmmCommands.CancelLoadVrm,
                _ => previewCanvas.Hide()
                );
        }

        private void LoadModelForPreview(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            TryWithoutException(() =>
            {
                if (Path.GetExtension(path).ToLower() == ".vrm")
                {
                    using (var context = new VRMImporterContext())
                    {
                        context.ParseGlb(File.ReadAllBytes(path));
                        _previewCanvas.Show(context);
                    }
                }
                else
                {
                    LogOutput.Instance.Write("unknown file type: " + path);
                }
            });
        }

        private void LoadModel(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            if (Path.GetExtension(path).ToLower() != ".vrm")
            {
                LogOutput.Instance.Write($"unknown file type: {path}");
                return;
            }

            TryWithoutException(() =>
            {
                var context = new VRMImporterContext();
                var file = File.ReadAllBytes(path);
                context.ParseGlb(file);

                context.Load();
                context.EnableUpdateWhenOffscreen();
                context.ShowMeshes();
                SetModel(context.Root);
            });
        }

        public void OnVrmLoadedFromVRoidHub(string modelId, GameObject vrmObject)
        {
            SetModel(vrmObject);
        }

        //モデルの破棄
        private void ReleaseCurrentVrm()
        {
            var loaded = _humanPoseTransferTarget;
            _humanPoseTransferTarget = null;

            if (loaded != null)
            {
                VrmDisposing?.Invoke();
                Destroy(loaded.gameObject);
            }
        }

        private void SetModel(GameObject go)
        {
            ReleaseCurrentVrm();

            if (go == null)
            {
                return;
            }

            var lookAt = go.GetComponent<VRMLookAtHead>();
            _humanPoseTransferTarget = go.AddComponent<HumanPoseTransfer>();
            _humanPoseTransferTarget.SourceType = HumanPoseTransfer.HumanPoseTransferSourceType.None;
            lookAt.UpdateType = UpdateType.LateUpdate;
            
            //セットアップのうちFinalIKに思い切り依存した所が別スクリプトになってます
            VRMLoadControllerHelper.SetupVrm(go, _ikTargets);

            var animator = go.GetComponent<Animator>();
            animator.runtimeAnimatorController = animatorController;
            
            var blendShapeProxy = go.GetComponent<VRMBlendShapeProxy>();
            
            var renderers = go.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                //セルフシャドウは明示的に切る: ちょっとでも軽量化したい
                r.receiveShadows = false;
            }
            
            var info = new VrmLoadedInfo()
            {
                vrmRoot = go.transform,
                animator = animator,
                blendShape = blendShapeProxy,
                renderers = renderers,
            };
            
            PreVrmLoaded?.Invoke(info);
            VrmLoaded?.Invoke(info);
        }
        
        [Serializable]
        public struct VrmLoadSetting
        {
            public Transform bodyTarget;
            public Transform leftHandTarget;
            public Transform rightHandTarget;
            public Transform rightIndexTarget;
            public Transform headTarget;
        }
    }
    
    [Serializable]
    public struct VrmLoadedInfo
    {
        public Transform vrmRoot;
        public Animator animator;
        public VRMBlendShapeProxy blendShape;
        public Renderer[] renderers;
    }
}
