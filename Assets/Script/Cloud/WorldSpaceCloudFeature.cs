using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class WorldSpaceCloudFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class CloudSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        public string shaderName = "Hidden/WorldSpaceClouds";
    }

    public CloudSettings settings = new CloudSettings();
    private WorldSpaceCloudPass cloudPass;
    private Material cloudMaterial;

    public override void Create()
    {
        Shader shader = Shader.Find(settings.shaderName);
        if (shader == null)
        {
            Debug.LogError($"World Space Clouds: Shader '{settings.shaderName}' not found!");
            return;
        }

        if (cloudMaterial == null)
        {
            cloudMaterial = CoreUtils.CreateEngineMaterial(shader);
        }

        cloudPass = new WorldSpaceCloudPass(cloudMaterial);
        cloudPass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (cloudPass != null && cloudMaterial != null)
        {
            var stack = VolumeManager.instance.stack;
            var cloudVolume = stack.GetComponent<WorldSpaceCloudsVolume>();

            if (cloudVolume != null && cloudVolume.IsValid())
            {
                cloudPass.SetVolume(cloudVolume);
                renderer.EnqueuePass(cloudPass);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (cloudMaterial != null)
                CoreUtils.Destroy(cloudMaterial);

            cloudPass?.Dispose();
        }
    }

    class WorldSpaceCloudPass : ScriptableRenderPass
    {
        private Material material;
        private RTHandle tempTarget;
        private WorldSpaceCloudsVolume volume;

        private static readonly int CloudTex1ID = Shader.PropertyToID("_CloudTex1");
        private static readonly int CloudTex2ID = Shader.PropertyToID("_CloudTex2");
        private static readonly int CloudTex3ID = Shader.PropertyToID("_CloudTex3");
        private static readonly int CloudColorID = Shader.PropertyToID("_CloudColor");
        private static readonly int CloudShadowColorID = Shader.PropertyToID("_CloudShadowColor");
        private static readonly int CloudHighlightColorID = Shader.PropertyToID("_CloudHighlightColor");
        private static readonly int CloudScaleID = Shader.PropertyToID("_CloudScale");
        private static readonly int CloudSpeedID = Shader.PropertyToID("_CloudSpeed");
        private static readonly int CloudHeightID = Shader.PropertyToID("_CloudHeight");
        private static readonly int CloudThicknessID = Shader.PropertyToID("_CloudThickness");
        private static readonly int DensityMultiplierID = Shader.PropertyToID("_DensityMultiplier");
        private static readonly int StepCountID = Shader.PropertyToID("_StepCount");
        private static readonly int LightAbsorptionID = Shader.PropertyToID("_LightAbsorption");
        private static readonly int DetailScaleID = Shader.PropertyToID("_DetailScale");
        private static readonly int DetailStrengthID = Shader.PropertyToID("_DetailStrength");
        private static readonly int SunDirectionID = Shader.PropertyToID("_SunDirection");
        private static readonly int AmbientLightID = Shader.PropertyToID("_AmbientLight");
        private static readonly int EdgeSoftnessID = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int PuffinessID = Shader.PropertyToID("_Puffiness");

        public WorldSpaceCloudPass(Material mat)
        {
            material = mat;
        }

        public void SetVolume(WorldSpaceCloudsVolume vol)
        {
            volume = vol;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ConfigureTarget(renderingData.cameraData.renderer.cameraColorTargetHandle);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || volume == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("World Space Clouds");

            // Set material parameters from volume - note the .value for Texture3DParameter
            material.SetTexture(CloudTex1ID, volume.cloudTexture1.value);
            material.SetTexture(CloudTex2ID, volume.cloudTexture2.value);
            material.SetTexture(CloudTex3ID, volume.cloudTexture3.value);
            material.SetColor(CloudColorID, volume.cloudColor.value);
            material.SetColor(CloudShadowColorID, volume.cloudShadowColor.value);
            material.SetColor(CloudHighlightColorID, volume.cloudHighlightColor.value);
            material.SetFloat(CloudScaleID, volume.cloudScale.value);
            material.SetVector(CloudSpeedID, volume.cloudSpeed.value);
            material.SetFloat(CloudHeightID, volume.cloudHeight.value);
            material.SetFloat(CloudThicknessID, volume.cloudThickness.value);
            material.SetFloat(DensityMultiplierID, volume.densityMultiplier.value);
            material.SetInt(StepCountID, volume.rayMarchSteps.value);
            material.SetFloat(LightAbsorptionID, volume.lightAbsorption.value);
            material.SetFloat(DetailScaleID, volume.detailScale.value);
            material.SetFloat(DetailStrengthID, volume.detailStrength.value);
            material.SetVector(SunDirectionID, volume.sunDirection.value.normalized);
            material.SetFloat(AmbientLightID, volume.ambientLight.value);
            material.SetFloat(EdgeSoftnessID, volume.edgeSoftness.value);
            material.SetFloat(PuffinessID, volume.puffiness.value);

            // Camera descriptor
            var cameraData = renderingData.cameraData;
            var descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

            RenderingUtils.ReAllocateIfNeeded(ref tempTarget, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TempCloudRT");

            Blitter.BlitCameraTexture(cmd, cameraData.renderer.cameraColorTargetHandle, tempTarget, material, 0);
            Blitter.BlitCameraTexture(cmd, tempTarget, cameraData.renderer.cameraColorTargetHandle);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            tempTarget?.Release();
        }
    }
}