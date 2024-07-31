using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
class DitherSettings
{
    public DitherSettings() { }
    public DitherSettings(DitherSettings other) 
    {
        _Strength = other._Strength;
        _ColorDepth = other._ColorDepth;
        _RandomRate = other._RandomRate;
        _DownsampleRatio = other._DownsampleRatio;
        _DownsampleFilterMode = other._DownsampleFilterMode;

    }
    [SerializeField, Range(0.0f, 10.0f)]
    float _Strength = .05f;
    public float Strength
    {
        get => _Strength;
        set => _Strength = Mathf.Clamp(value, 0.0f, 10.0f);
        //set => _Strength = value;

    }
    [SerializeField, Range(2, 256)]
    int _ColorDepth = 256;
    public int ColorDepth
    {
        get => _ColorDepth;
        set => _ColorDepth = Mathf.Clamp(value, 2, 256);
    }
    [SerializeField, Range(0.0f, 1.0f)]
    float _RandomRate = 0.0f;
    public float RandomRate
    {
        get => _RandomRate;
        set => _RandomRate = Mathf.Clamp(0.0f, 1.0f, value);
    }
    [SerializeField, Min(1)]
    int _DownsampleRatio = 1;
    public int DownsampleRatio
    {
        get => _DownsampleRatio;
        set => _DownsampleRatio = Mathf.Max(value, 1);
    }
    [SerializeField]
    private FilterMode _DownsampleFilterMode = FilterMode.Point;
    public FilterMode DownsampleFilterMode
    {
        get => _DownsampleFilterMode;
        set => _DownsampleFilterMode = value;
    }

    [SerializeField]
    private RenderPassEvent _RenderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    public RenderPassEvent RenderPassEvent
    {
        get => _RenderPassEvent;
        set => _RenderPassEvent = value;
    }
    [SerializeField]
    private string _ProfilerTag = "Dither Renderer Feature";
    public string ProfilerTag
    {
        get => _ProfilerTag;
        set => _ProfilerTag = value;
    }
}
class DitherRendererFeature : ScriptableRendererFeature
{
    // Serialized Fields
    [SerializeField, HideInInspector]
    private Shader m_DitherShader;
    [SerializeField]
    private DitherSettings m_Settings = new DitherSettings();
    [SerializeField]
    private CameraType m_CameraType = CameraType.SceneView | CameraType.Game;

    // Private Fields
    private DitherPass m_DitherPass = null;
    private bool m_Initialized = false;
    private Material m_DitherMaterial;

    // Constants
    private const string k_ShaderPath = "Shaders/";
    private const string k_DitherShaderName = "Dither";

    public DitherSettings GetSettings()
    {
        return new DitherSettings(m_Settings);
    }
    public void SetSettings(DitherSettings settings)
    {
        m_Settings = settings;
    }

    public override void Create()
    {
        if (!RendererFeatureHelper.ValidUniversalPipeline(GraphicsSettings.defaultRenderPipeline, true, false)) return;
        
        m_Initialized = Initialize();

        if (m_Initialized)
            if (m_DitherPass == null)
                m_DitherPass = new DitherPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!m_Initialized) return;

        if (!RendererFeatureHelper.CameraTypeMatches(m_CameraType, renderingData.cameraData.cameraType)) return;

        bool shouldAdd = m_DitherPass.Setup(m_Settings, renderer, m_DitherMaterial);
        if (shouldAdd)
        {
            renderer.EnqueuePass(m_DitherPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        m_DitherPass.Dispose();
        RendererFeatureHelper.DisposeMaterial(ref m_DitherMaterial);
        base.Dispose(disposing);
    }

    private bool Initialize()
    {
        if (!RendererFeatureHelper.LoadShader(ref m_DitherShader, k_ShaderPath, k_DitherShaderName)) return false;
        if (!RendererFeatureHelper.GetMaterial(m_DitherShader, ref m_DitherMaterial)) return false;
        return true;
    }


    class DitherPass : ScriptableRenderPass
    {
        // Private Variables
        private Material m_DitherMaterial;
        RenderTargetIdentifier m_ResizeTextureTarget;
        RenderTargetIdentifier m_TempTextureTarget;
        private ProfilingSampler m_ProfilingSampler = null;
        private ScriptableRenderer m_Renderer = null;
        private DitherSettings m_CurrentSettings = new DitherSettings();

        // Constants
        private const string k_PassProfilerTag = "Dither Pass";

        // Statics
        private static readonly int s_ResizeTextureID = Shader.PropertyToID("_Dither_ResizeTex");
        private static readonly int s_TempTextureID = Shader.PropertyToID("_Dither_TempTex");

        public DitherPass() { }

        public bool Setup(DitherSettings settings, ScriptableRenderer renderer, Material ditherMaterial)
        {
            m_CurrentSettings = settings;
            m_Renderer = renderer;
            m_DitherMaterial = ditherMaterial;

            m_ProfilingSampler = new ProfilingSampler(k_PassProfilerTag);
            renderPassEvent = m_CurrentSettings.RenderPassEvent;
            ConfigureInput(ScriptableRenderPassInput.Color);

            if (m_DitherMaterial == null) return false;
            return true;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

        // called each frame before Execute, use it to set up things the pass will need
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor renderTextureDescriptor = new RenderTextureDescriptor(
                Mathf.CeilToInt(cameraTextureDescriptor.width / m_CurrentSettings.DownsampleRatio),
                Mathf.CeilToInt(cameraTextureDescriptor.height / m_CurrentSettings.DownsampleRatio),
                cameraTextureDescriptor.colorFormat
                );
            cmd.GetTemporaryRT(s_ResizeTextureID, renderTextureDescriptor, m_CurrentSettings.DownsampleFilterMode);
            m_ResizeTextureTarget = new RenderTargetIdentifier(s_ResizeTextureID);
            ConfigureTarget(m_ResizeTextureTarget);

            cmd.GetTemporaryRT(s_TempTextureID, renderTextureDescriptor, m_CurrentSettings.DownsampleFilterMode);
            m_TempTextureTarget = new RenderTargetIdentifier(s_TempTextureID);
            ConfigureTarget(m_TempTextureTarget);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // fetch a command buffer to use
            CommandBuffer cmd = CommandBufferPool.Get(m_CurrentSettings.ProfilerTag);
            using(new ProfilingScope(cmd, m_ProfilingSampler))
            {
                m_DitherMaterial.SetFloat("_Strength", m_CurrentSettings.Strength);
                m_DitherMaterial.SetInt("_ColorDepth", m_CurrentSettings.ColorDepth);
                m_DitherMaterial.SetInt("_DownsampleRatio", m_CurrentSettings.DownsampleRatio);
                m_DitherMaterial.SetFloat("_RandomRate", m_CurrentSettings.RandomRate);

                // where the render pass does its work
                cmd.Blit(m_Renderer.cameraColorTarget, m_ResizeTextureTarget);
                //cmd.Blit(m_Renderer.cameraColorTarget, m_TempTextureTarget, m_DitherMaterial, 0);
                cmd.Blit(m_ResizeTextureTarget, m_TempTextureTarget, m_DitherMaterial, 0);

                // then blit back into color target 
                cmd.Blit(m_TempTextureTarget, m_Renderer.cameraColorTarget);
            }

            // don't forget to tell ScriptableRenderContext to actually execute the commands
            context.ExecuteCommandBuffer(cmd);

            // tidy up after ourselves
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // Release Temporary RT here
            cmd.ReleaseTemporaryRT(s_TempTextureID);
        }

        public void Dispose()
        {
            // Dispose of buffers here
            // this pass doesnt have any buffers
        }
    }
}
